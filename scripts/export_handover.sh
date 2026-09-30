#!/usr/bin/env bash
# 인계용 저장소를 만든다.
# 지정한 커밋의 파일만 새 폴더에 풀고, 내부 작업 기록과 개발 도구 설정을 뺀 뒤 커밋 1개짜리 저장소로 만든다.
# 이 저장소(원본)는 읽기만 하고 아무것도 바꾸지 않는다.
#
# 사용법 (Git Bash):
#   scripts/export_handover.sh <출력 폴더> [커밋, 기본 HEAD]
# 예:
#   scripts/export_handover.sh /d/Handover/DataMeasurement

set -euo pipefail

OUT_DIR="${1:?출력 폴더를 지정하세요}"
REF="${2:-HEAD}"
SRC_ROOT="$(git rev-parse --show-toplevel)"

if [ -e "$OUT_DIR" ]; then
  echo "이미 존재하는 경로입니다: $OUT_DIR" >&2
  exit 1
fi

# 인계본에서 빼는 경로 — 내부 작업 기록, 개발 도구 설정, 이미 끝난 일회성 변환 도구
EXCLUDES=(
  ".planning"
  ".claude"
  "CLAUDE.md"
  "scripts/export_handover.sh"
  "scripts/migrate_phase73_recipe.py"
)

mkdir -p "$OUT_DIR"
git -C "$SRC_ROOT" archive --format=tar "$REF" | tar -x -C "$OUT_DIR"

for szPath in "${EXCLUDES[@]}"; do
  rm -rf "${OUT_DIR:?}/${szPath:?}"
done
rmdir "${OUT_DIR:?}/scripts" 2>/dev/null || true

# .gitignore 에서 개발 도구 전용 블록(빈 줄 포함)을 뺀다
if [ -f "$OUT_DIR/.gitignore" ]; then
  sed -i '/^# Claude Code harness/,/^!\.claude\/hooks\/\*$/d' "$OUT_DIR/.gitignore"
  sed -i '/^\.claude\//d; /^!\.claude\//d' "$OUT_DIR/.gitignore"
fi

szVersion="$(grep -o 'VERSION = "[^"]*"' "$OUT_DIR/WPF_Example/VersionDefine.cs" | head -1 | cut -d'"' -f2)"
szVersion="${szVersion:-unknown}"

git -C "$OUT_DIR" init -q
# packages/ 아래 경로가 길어 Windows 260자 제한에 걸리는 파일이 있다
git -C "$OUT_DIR" config core.longpaths true
git -C "$OUT_DIR" add -A
git -C "$OUT_DIR" commit -q -m "DataMeasurement ${szVersion}"

echo "완료: $OUT_DIR"
echo "  기준 커밋 : $(git -C "$SRC_ROOT" rev-parse --short "$REF")"
echo "  버전      : ${szVersion}"
echo "  파일 수   : $(git -C "$OUT_DIR" ls-files | wc -l)"
echo "  커밋 수   : $(git -C "$OUT_DIR" rev-list --count HEAD)"
