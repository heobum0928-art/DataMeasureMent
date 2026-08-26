# -*- coding: utf-8 -*-
"""
Phase 73 — SIDE 단일 시퀀스를 SIDE_1~4 네 지그로 분리하기 위한 main.ini 마이그레이션.

원본은 절대 덮어쓰지 않는다. 새 파일로만 출력하고, 무결성 카운트가 하나라도
어긋나면 출력 파일을 만들지 않고 종료 코드 1 로 끝낸다(부분 출력 금지).

바꾸는 것은 딱 세 가지다.
  1. [SHOT_n_CAM] 8개의 OwnerSequenceName / ZIndex
  2. [FIXTURE_SIDE_DATUM_k] 4개의 섹션 이름과 ZIndexA / ZIndexB
  3. [FIXTURE_SIDE] 한 섹션을 [FIXTURE_SIDE_1..4] 네 섹션으로 교체

건드리지 않는 것 (의도적)
  - [Param0..7] 레거시 섹션. TryLoadNewFormat 이 성공하면 읽히지 않는 죽은 경로이고
    다음 저장 때 자연 소멸한다. 유일본 레시피를 불필요하게 건드리지 않는다.
  - Datum 섹션의 그 밖의 모든 키(RefMatch / PatternRoi / TeachingImagePath / OwnerName 등).
    OwnerName 은 ParamBase 의 읽기 전용 계산 프로퍼티라 로드 시 무시되고,
    다음 저장 때 소속 시퀀스 이름으로 다시 쓰인다.

INI 파서를 쓰지 않고 라인 단위로 편집한다. configparser 계열은 재작성하면서
키 순서·중복·주석·개행을 조용히 잃는다.

사용법:
  python scripts/migrate_phase73_recipe.py --in <원본> --out <새 파일>
"""

from __future__ import print_function

import argparse
import io
import re
import sys


# --- 하드코딩 변환 테이블 (추론 금지, 73-03-PLAN <interfaces> 실측값) ---

# 섹션 이름(_CAM 접미사 제외) -> (신규 OwnerSequenceName, 신규 ZIndex)
SHOT_MAP = {
    "SHOT_4":  ("SIDE_1", 2),
    "SHOT_6":  ("SIDE_2", 2),
    "SHOT_26": ("SIDE_2", 3),
    "SHOT_3":  ("SIDE_3", 2),
    "SHOT_23": ("SIDE_3", 3),
    "SHOT_24": ("SIDE_3", 4),
    "SHOT_5":  ("SIDE_4", 2),
    "SHOT_25": ("SIDE_4", 3),
}

# 구 Datum 섹션 -> (신규 섹션, 신규 ZIndexA, 신규 ZIndexB)
DATUM_MAP = {
    "FIXTURE_SIDE_DATUM_0": ("FIXTURE_SIDE_1_DATUM_0", 0, 1),
    "FIXTURE_SIDE_DATUM_1": ("FIXTURE_SIDE_2_DATUM_0", 0, 1),
    "FIXTURE_SIDE_DATUM_2": ("FIXTURE_SIDE_3_DATUM_0", 0, 1),
    "FIXTURE_SIDE_DATUM_3": ("FIXTURE_SIDE_4_DATUM_0", 0, 1),
}

# 지그별 z 오프셋 — ZIndexA / ZIndexB 가 SHOT_*_CAM 에 존재할 때만 쓰인다(현 레시피엔 없음)
JIG_Z_OFFSET = {
    "SIDE_1": 0,
    "SIDE_2": 3,
    "SIDE_3": 7,
    "SIDE_4": 12,
}

# [FIXTURE_SIDE] 한 섹션을 대체할 네 섹션. DisplayName 은 지그마다 달라야 한다
# (넷 다 "SIDE" 로 두면 트리 노드 4개가 같은 이름이 되어 구분이 안 된다).
NEW_FIXTURE_SECTIONS = [
    ("FIXTURE_SIDE_1", "SIDE_1 (Datum 3-1)", 1),
    ("FIXTURE_SIDE_2", "SIDE_2 (Datum 3-2)", 1),
    ("FIXTURE_SIDE_3", "SIDE_3 (Datum 4-2)", 1),
    ("FIXTURE_SIDE_4", "SIDE_4 (Datum 4-1)", 1),
]

LEGACY_FIXTURE_SECTION = "FIXTURE_SIDE"

SIDE_SHOT_SECTIONS = sorted(SHOT_MAP.keys())

# 신규 z 커버리지 기대값 (D-73-01 — 빈칸 없이 연속)
EXPECTED_Z_COVERAGE = {
    "SIDE_1": set([0, 1, 2]),
    "SIDE_2": set([0, 1, 2, 3]),
    "SIDE_3": set([0, 1, 2, 3, 4]),
    "SIDE_4": set([0, 1, 2, 3]),
}

SECTION_RE = re.compile(r"^\[(.+)\]$")
SHOT_CAM_RE = re.compile(r"^SHOT_[0-9]+_CAM$")
SHOT_PLAIN_RE = re.compile(r"^SHOT_[0-9]+$")
PARAM_RE = re.compile(r"^Param[0-9]+$")
MEAS_RE = re.compile(r"^SHOT_([0-9]+)_FAI_[0-9]+_MEAS_[0-9]+$")
FAI_RE = re.compile(r"^SHOT_[0-9]+_FAI_[0-9]+$")


def split_lines_keep_cr(text):
    """'\\n' 으로만 쪼갠다. 각 원소 끝에 남는 '\\r' 이 CRLF 여부를 그대로 보존한다."""
    return text.split("\n")


def join_lines_keep_cr(lines):
    return "\n".join(lines)


def parse_section_name(line_body):
    match = SECTION_RE.match(line_body)
    if match is None:
        return None
    return match.group(1)


def strip_cr(line):
    if line.endswith("\r"):
        return line[:-1], True
    return line, False


def make_line(body, had_cr):
    if had_cr:
        return body + "\r"
    return body


# ---------------------------------------------------------------- 변환

def transform(text):
    """편집된 전체 텍스트를 돌려준다. 원본 문자열은 변경하지 않는다."""
    lines = split_lines_keep_cr(text)
    out_lines = []

    current_section = None
    current_jig = None       # 현재 SHOT_*_CAM 섹션이 속하게 될 지그 이름
    skipping_legacy = False  # [FIXTURE_SIDE] 본문 스킵 중인가

    # 파일 대부분이 CRLF 라 삽입 라인도 CRLF 로 맞춘다.
    insert_cr = text.find("\r\n") >= 0

    for raw_line in lines:
        body, had_cr = strip_cr(raw_line)
        section_name = parse_section_name(body)

        if section_name is not None:
            # 새 섹션 시작 — 레거시 스킵 상태를 여기서 푼다
            if skipping_legacy:
                skipping_legacy = False

            if section_name == LEGACY_FIXTURE_SECTION:
                # 구 섹션 자리에 신규 4개를 삽입하고 본문은 통째로 스킵
                for new_name, display_name, datum_count in NEW_FIXTURE_SECTIONS:
                    out_lines.append(make_line("[" + new_name + "]", insert_cr))
                    out_lines.append(make_line("DisplayName=" + display_name, insert_cr))
                    out_lines.append(make_line("DatumCount=" + str(datum_count), insert_cr))
                    out_lines.append(make_line("", insert_cr))
                skipping_legacy = True
                current_section = section_name
                current_jig = None
                continue

            if section_name in DATUM_MAP:
                new_section = DATUM_MAP[section_name][0]
                out_lines.append(make_line("[" + new_section + "]", had_cr))
                current_section = section_name
                current_jig = None
                continue

            current_section = section_name
            current_jig = None
            if SHOT_CAM_RE.match(section_name):
                shot_key = section_name[:-len("_CAM")]
                if shot_key in SHOT_MAP:
                    current_jig = SHOT_MAP[shot_key][0]
            out_lines.append(raw_line)
            continue

        if skipping_legacy:
            # [FIXTURE_SIDE] 본문(DisplayName / DatumCount / 빈 줄) 제거
            continue

        # --- 키 라인 편집 ---
        if current_section is not None and SHOT_CAM_RE.match(current_section):
            shot_key = current_section[:-len("_CAM")]
            if shot_key in SHOT_MAP:
                new_owner, new_z = SHOT_MAP[shot_key]
                if body.startswith("OwnerSequenceName="):
                    out_lines.append(make_line("OwnerSequenceName=" + new_owner, had_cr))
                    continue
                if body.startswith("ZIndex="):
                    out_lines.append(make_line("ZIndex=" + str(new_z), had_cr))
                    continue
                if body.startswith("ZIndexA=") or body.startswith("ZIndexB="):
                    key, _, value = body.partition("=")
                    shifted = shift_z_value(value, current_jig)
                    if shifted is None:
                        out_lines.append(raw_line)
                    else:
                        out_lines.append(make_line(key + "=" + str(shifted), had_cr))
                    continue

        if current_section is not None and current_section in DATUM_MAP:
            _, new_za, new_zb = DATUM_MAP[current_section]
            if body.startswith("ZIndexA="):
                out_lines.append(make_line("ZIndexA=" + str(new_za), had_cr))
                continue
            if body.startswith("ZIndexB="):
                out_lines.append(make_line("ZIndexB=" + str(new_zb), had_cr))
                continue

        out_lines.append(raw_line)

    return join_lines_keep_cr(out_lines)


def shift_z_value(value_text, jig_name):
    """미설정(-1) 이거나 파싱 불가면 None 을 돌려 원본 라인을 그대로 두게 한다."""
    if jig_name is None:
        return None
    if jig_name not in JIG_Z_OFFSET:
        return None
    stripped = value_text.strip()
    try:
        value = int(stripped)
    except ValueError:
        return None
    if value < 0:
        return None
    return value - JIG_Z_OFFSET[jig_name]


# ---------------------------------------------------------------- 집계

def collect(text):
    """무결성 비교용 통계를 모은다."""
    stats = {
        "section_count": 0,
        "shot_sections": 0,
        "fai_sections": 0,
        "meas_sections": 0,
        "side_shot_meas": 0,
        "owner_total": 0,
        "owner_in_shotcam": 0,
        "owner_in_param": 0,
        "owner_shotcam_by_value": {},
        "owner_param_by_value": {},
        "datum_sections": [],
        "datum_key_counts": {},
        "param_sections": [],
        "param_key_counts": {},
        "fixture_side_legacy": 0,
        "fixture_side_new": [],
        "fixture_side_display": {},
        "fixture_side_datumcount": {},
        "z_by_jig": {},
        "cr_count": text.count("\r"),
        "lf_count": text.count("\n"),
    }

    side_shot_numbers = set()
    for name in SIDE_SHOT_SECTIONS:
        side_shot_numbers.add(name[len("SHOT_"):])

    current_section = None
    for raw_line in split_lines_keep_cr(text):
        body, _ = strip_cr(raw_line)
        section_name = parse_section_name(body)

        if section_name is not None:
            current_section = section_name
            stats["section_count"] += 1
            if SHOT_PLAIN_RE.match(section_name):
                stats["shot_sections"] += 1
            if FAI_RE.match(section_name):
                stats["fai_sections"] += 1
            meas_match = MEAS_RE.match(section_name)
            if meas_match is not None:
                stats["meas_sections"] += 1
                if meas_match.group(1) in side_shot_numbers:
                    stats["side_shot_meas"] += 1
            if section_name.startswith("FIXTURE_SIDE_DATUM_"):
                stats["datum_sections"].append(section_name)
                stats["datum_key_counts"][section_name] = 0
            if re.match(r"^FIXTURE_SIDE_[1-4]_DATUM_[0-9]+$", section_name):
                stats["datum_sections"].append(section_name)
                stats["datum_key_counts"][section_name] = 0
            if section_name == LEGACY_FIXTURE_SECTION:
                stats["fixture_side_legacy"] += 1
            if re.match(r"^FIXTURE_SIDE_[1-4]$", section_name):
                stats["fixture_side_new"].append(section_name)
            if PARAM_RE.match(section_name):
                stats["param_sections"].append(section_name)
                stats["param_key_counts"][section_name] = 0
            continue

        if body.strip() == "":
            continue

        if current_section is None:
            continue

        if current_section in stats["datum_key_counts"]:
            stats["datum_key_counts"][current_section] += 1
        if current_section in stats["param_key_counts"]:
            stats["param_key_counts"][current_section] += 1

        if body.startswith("OwnerSequenceName="):
            value = body.partition("=")[2].strip()
            stats["owner_total"] += 1
            if SHOT_CAM_RE.match(current_section):
                stats["owner_in_shotcam"] += 1
                stats["owner_shotcam_by_value"][value] = \
                    stats["owner_shotcam_by_value"].get(value, 0) + 1
            if PARAM_RE.match(current_section):
                stats["owner_in_param"] += 1
                stats["owner_param_by_value"][value] = \
                    stats["owner_param_by_value"].get(value, 0) + 1

        if re.match(r"^FIXTURE_SIDE_[1-4]$", current_section):
            if body.startswith("DisplayName="):
                stats["fixture_side_display"][current_section] = body.partition("=")[2]
            if body.startswith("DatumCount="):
                stats["fixture_side_datumcount"][current_section] = body.partition("=")[2].strip()

    collect_z_coverage(text, stats)
    return stats


def collect_z_coverage(text, stats):
    """신규 지그별 z 커버리지 = Datum ZIndexA/B + 소속 Shot 의 ZIndex."""
    coverage = {}
    current_section = None
    for raw_line in split_lines_keep_cr(text):
        body, _ = strip_cr(raw_line)
        section_name = parse_section_name(body)
        if section_name is not None:
            current_section = section_name
            continue
        if current_section is None:
            continue

        datum_match = re.match(r"^FIXTURE_(SIDE_[1-4])_DATUM_[0-9]+$", current_section)
        if datum_match is not None:
            jig = datum_match.group(1)
            if body.startswith("ZIndexA=") or body.startswith("ZIndexB="):
                value_text = body.partition("=")[2].strip()
                try:
                    value = int(value_text)
                except ValueError:
                    continue
                if value >= 0:
                    coverage.setdefault(jig, set()).add(value)
            continue

        if SHOT_CAM_RE.match(current_section):
            if body.startswith("ZIndex="):
                value_text = body.partition("=")[2].strip()
                try:
                    value = int(value_text)
                except ValueError:
                    continue
                owner = shot_cam_owner(text, current_section)
                if owner in EXPECTED_Z_COVERAGE:
                    coverage.setdefault(owner, set()).add(value)

    stats["z_by_jig"] = coverage


_OWNER_CACHE = {}


def shot_cam_owner(text, section_name):
    key = (id(text), section_name)
    if key in _OWNER_CACHE:
        return _OWNER_CACHE[key]
    owner = None
    inside = False
    for raw_line in split_lines_keep_cr(text):
        body, _ = strip_cr(raw_line)
        found = parse_section_name(body)
        if found is not None:
            inside = (found == section_name)
            continue
        if inside and body.startswith("OwnerSequenceName="):
            owner = body.partition("=")[2].strip()
            break
    _OWNER_CACHE[key] = owner
    return owner


# ---------------------------------------------------------------- 검증

class Report(object):
    def __init__(self):
        self.rows = []
        self.failed = 0

    def check(self, name, before, after, expected):
        ok = (after == expected)
        if not ok:
            self.failed += 1
        self.rows.append((name, before, after, expected, ok))

    def note(self, name, before, after):
        self.rows.append((name, before, after, "(참고)", True))

    def render(self):
        width_name = 0
        for row in self.rows:
            if len(row[0]) > width_name:
                width_name = len(row[0])
        lines = []
        header = "%-*s | %-22s | %-22s | %-22s | %s" % (
            width_name, "항목", "편집 전", "편집 후", "기대값", "판정")
        lines.append(header)
        lines.append("-" * len(header))
        for name, before, after, expected, ok in self.rows:
            if ok:
                verdict = "OK"
            else:
                verdict = "FAIL"
            lines.append("%-*s | %-22s | %-22s | %-22s | %s" % (
                width_name, name, str(before), str(after), str(expected), verdict))
        return "\n".join(lines)


def verify(before, after):
    report = Report()

    report.check("전체 섹션 수", before["section_count"], after["section_count"],
                 before["section_count"] + 3)
    report.check("SHOT_n 섹션 수", before["shot_sections"], after["shot_sections"],
                 before["shot_sections"])
    report.check("SHOT_*_CAM Owner 총 개수", before["owner_in_shotcam"],
                 after["owner_in_shotcam"], before["owner_in_shotcam"])
    report.check("파일 전체 Owner 총 개수", before["owner_total"], after["owner_total"],
                 before["owner_total"])

    before_side = before["owner_shotcam_by_value"].get("SIDE", 0)
    after_side = after["owner_shotcam_by_value"].get("SIDE", 0)
    report.check("SHOT_*_CAM Owner==SIDE", before_side, after_side, 0)

    report.check("[ParamN] Owner==SIDE (무접촉)",
                 before["owner_param_by_value"].get("SIDE", 0),
                 after["owner_param_by_value"].get("SIDE", 0),
                 before["owner_param_by_value"].get("SIDE", 0))

    expected_jig_counts = {"SIDE_1": 1, "SIDE_2": 2, "SIDE_3": 3, "SIDE_4": 2}
    for jig in ["SIDE_1", "SIDE_2", "SIDE_3", "SIDE_4"]:
        report.check("SHOT_*_CAM Owner==" + jig,
                     before["owner_shotcam_by_value"].get(jig, 0),
                     after["owner_shotcam_by_value"].get(jig, 0),
                     expected_jig_counts[jig])

    for owner in ["TOP", "BOTTOM"]:
        report.check("SHOT_*_CAM Owner==" + owner,
                     before["owner_shotcam_by_value"].get(owner, 0),
                     after["owner_shotcam_by_value"].get(owner, 0),
                     before["owner_shotcam_by_value"].get(owner, 0))

    report.check("SIDE Datum 섹션 수", len(before["datum_sections"]),
                 len(after["datum_sections"]), len(before["datum_sections"]))

    # 섹션 이름이 바뀌므로 DATUM_MAP 으로 짝지어 키 개수를 비교한다
    datum_key_mismatch = 0
    for old_name in sorted(DATUM_MAP.keys()):
        new_name = DATUM_MAP[old_name][0]
        old_keys = before["datum_key_counts"].get(old_name, -1)
        new_keys = after["datum_key_counts"].get(new_name, -2)
        if old_keys != new_keys:
            datum_key_mismatch += 1
    report.check("Datum 섹션별 키 개수 불일치", 0, datum_key_mismatch, 0)

    report.check("[ParamN] 섹션 개수", len(before["param_sections"]),
                 len(after["param_sections"]), len(before["param_sections"]))

    param_key_mismatch = 0
    for name in before["param_key_counts"]:
        if before["param_key_counts"][name] != after["param_key_counts"].get(name, -1):
            param_key_mismatch += 1
    report.check("[ParamN] 키 개수 불일치", 0, param_key_mismatch, 0)

    report.check("구 [FIXTURE_SIDE] 섹션", before["fixture_side_legacy"],
                 after["fixture_side_legacy"], 0)
    report.check("[FIXTURE_SIDE_1..4] 섹션 수", len(before["fixture_side_new"]),
                 len(after["fixture_side_new"]), 4)

    display_values = []
    for name in sorted(after["fixture_side_display"].keys()):
        display_values.append(after["fixture_side_display"][name])
    report.check("DisplayName 유일 개수", 0, len(set(display_values)), 4)

    datum_count_bad = 0
    for name in ["FIXTURE_SIDE_1", "FIXTURE_SIDE_2", "FIXTURE_SIDE_3", "FIXTURE_SIDE_4"]:
        if after["fixture_side_datumcount"].get(name, "") != "1":
            datum_count_bad += 1
    report.check("FIXTURE_SIDE_n DatumCount=1 위반", 0, datum_count_bad, 0)

    report.check("FAI 섹션 총 개수", before["fai_sections"], after["fai_sections"],
                 before["fai_sections"])
    report.check("_MEAS_ 섹션 총 개수", before["meas_sections"], after["meas_sections"],
                 before["meas_sections"])
    report.check("SIDE 소유 Shot 측정 섹션", before["side_shot_meas"],
                 after["side_shot_meas"], 25)

    # 개행 — CRLF 유지. LF-only 라인이 하나라도 생기면 CR < LF 가 된다.
    report.check("LF-only 라인 수 (입력)", before["lf_count"] - before["cr_count"],
                 before["lf_count"] - before["cr_count"], 0)
    report.check("LF-only 라인 수 (출력)", before["lf_count"] - before["cr_count"],
                 after["lf_count"] - after["cr_count"], 0)
    cr_increased = (after["cr_count"] >= before["cr_count"])
    if cr_increased:
        cr_flag = 0
    else:
        cr_flag = 1
    report.check("CR 감소 여부 (0=정상)", 0, cr_flag, 0)
    report.note("CR 바이트 수", before["cr_count"], after["cr_count"])

    z_bad = 0
    z_detail = []
    for jig in ["SIDE_1", "SIDE_2", "SIDE_3", "SIDE_4"]:
        actual = after["z_by_jig"].get(jig, set())
        if actual != EXPECTED_Z_COVERAGE[jig]:
            z_bad += 1
        z_detail.append(jig + "=" + str(sorted(actual)))
    report.check("신규 z 커버리지 불일치", 0, z_bad, 0)
    for detail in z_detail:
        report.note("  z " + detail.split("=")[0], "-", detail.split("=", 1)[1])

    return report


# ---------------------------------------------------------------- main

def main():
    parser = argparse.ArgumentParser(
        description="Phase 73 SIDE 4지그 분리 레시피 마이그레이션 (원본 무변경)")
    parser.add_argument("--in", dest="src", required=True, help="입력 main.ini (읽기 전용)")
    parser.add_argument("--out", dest="dst", required=True, help="출력 파일 (새로 만든다)")
    args = parser.parse_args()

    with io.open(args.src, "r", encoding="utf-8", newline="") as handle:
        source_text = handle.read()

    if source_text.startswith(u"﻿"):
        print("[중단] 입력 파일에 BOM 이 있다. 이 스크립트는 BOM 없는 UTF-8 만 다룬다.")
        return 1

    output_text = transform(source_text)

    before = collect(source_text)
    after = collect(output_text)
    report = verify(before, after)

    print("")
    print("=== Phase 73 레시피 마이그레이션 무결성 검증 ===")
    print("입력 : " + args.src)
    print("출력 : " + args.dst)
    print("")
    print(report.render())
    print("")

    if report.failed > 0:
        print("[실패] 무결성 항목 %d 건이 기대값과 다르다. 출력 파일을 만들지 않고 종료한다."
              % report.failed)
        return 1

    with io.open(args.dst, "w", encoding="utf-8", newline="") as handle:
        handle.write(output_text)

    print("[성공] 전 항목 일치. 출력 파일을 기록했다: " + args.dst)
    print("       원본은 건드리지 않았다. 교체는 diff 확인 + 사람 승인 후 수동으로 한다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
