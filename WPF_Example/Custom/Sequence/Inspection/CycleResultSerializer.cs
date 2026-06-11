using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReringProject.Halcon.Models;
using ReringProject.Network;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 검사 cycle 결과를 구조화 JSON 으로 저장/로드하는 정적 서비스.
    /// 리뷰어 재렌더와 xlsx export 의 공통 단일 데이터 소스.
    /// 저장 경로: ResultSavePath/{YYYYMMDD}/{HHmmss}_cycle/cycle.json.
    /// cycleDir 계산은 ResultSavePath + DateTime 포맷팅만 사용 — 외부 입력 없음, path traversal 불가.
    /// Load 는 TypeNameHandling.None 명시 + try/catch → null 반환 — RCE 방지.
    /// </summary>
    public static class CycleResultSerializer
    {
        /// <summary>
        /// InspectionRecipeManager 의 현재 Shot/FAI/Measurement 스냅샷을 CycleResultDto 로 빌드한다.
        /// AddResponse() 에서 종합판정 확정 직후 호출 — 전 FAI 결과가 채워진 시점.
        /// </summary>
        /// <param name="recipeManager">현재 InspectionRecipeManager (Shots 순회 대상)</param>
        /// <param name="cycleResult">AddResponse 에서 확정된 종합판정 (EVisionResultType)</param>
        /// <param name="when">검사 일시 (DateTime.Now — AddResponse 호출 시점)</param>
        /// <param name="recipeName">현재 레시피/모델명</param>
        /// <param name="ownerSequenceName">
        /// 이 cycle 을 생성한 시퀀스 이름(TOP/SIDE/BOTTOM). 지정 시 해당 시퀀스 소유 shot 만 포함한다.
        /// null/빈값이면 전체 shot 포함(레거시). 빈 OwnerSequenceName shot 은 "TOP" 으로 폴백 매칭 (SequenceHandler/ShotConfig 정책 일치).
        /// </param>
        public static CycleResultDto BuildDto(
            InspectionRecipeManager recipeManager,
            EVisionResultType cycleResult,
            DateTime when,
            string recipeName,
            string ownerSequenceName = null)
        {
            var dto = new CycleResultDto
            {
                InspectionTime = when,
                RecipeName = recipeName ?? "",
                OverallJudgement = MapJudgement(cycleResult)
                // CycleFolderPath 는 SaveAsync 에서 계산 후 설정
            };

            if (recipeManager == null)
            {
                return dto;
            }

            foreach (var shot in recipeManager.Shots)
            {
                // 시퀀스별 cycle: 실행한 시퀀스 소유 shot 만 포함.
                // 검사하지 않은 다른 시퀀스(예: BOTTOM 검사 시 TOP/SIDE) shot 이 cycle.json/리뷰어에 stale 로 찍히던 문제 해소.
                if (!string.IsNullOrEmpty(ownerSequenceName))
                {
                    string shotOwner = string.IsNullOrEmpty(shot.OwnerSequenceName) ? "TOP" : shot.OwnerSequenceName; // SequenceHandler.SEQ_TOP 폴백 정책 일치
                    if (!string.Equals(shotOwner, ownerSequenceName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                var shotDto = new ShotResultDto
                {
                    ShotName = shot.ShotName ?? "",
                    OwnerSequenceName = shot.OwnerSequenceName ?? "",
                    ResultImagePath = shot.GetLatestImagePath() ?? ""
                    // GetLatestImagePath() = SimulImagePath — 측정 소스 이미지 (리뷰어 재로드용)
                    // SaveResultImage SaveFailImage 게이트에 의존하지 않음
                };

                foreach (var fai in shot.FAIList)
                {
                    var faiDto = new FaiResultDto
                    {
                        FAIName = fai.FAIName ?? "",
                        IsPass = fai.IsPass,
                        WasDatumSkipped = fai.WasDatumSkipped,
                        OriginImageFileName = fai.LastOriginImageFileName ?? "",   // 검사 시점 write-back 파일명 복사
                        CaptureImageFileName = fai.LastCaptureImageFileName ?? "",
                        // FAIConfig.LastOverlays 는 [JsonIgnore] (INI 직렬화 제외용) —
                        // DTO 계층에서 별도 복사하여 JSON 직렬화 노출
                        LastOverlays = new List<EdgeInspectionOverlay>(
                            fai.LastOverlays != null ? fai.LastOverlays : new List<EdgeInspectionOverlay>())
                    };

                    foreach (var meas in fai.Measurements)
                    {
                        var measDto = new MeasurementResultDto
                        {
                            MeasurementName = string.IsNullOrEmpty(meas.MeasurementName)
                                ? meas.TypeName
                                : meas.MeasurementName,
                            TypeName = meas.TypeName ?? "",
                            NominalValue = meas.NominalValue,
                            TolerancePlus = meas.TolerancePlus,
                            ToleranceMinus = meas.ToleranceMinus,
                            LastMeasuredValue = meas.LastMeasuredValue,
                            LastJudgement = meas.LastJudgement,
                            LastHasResult = meas.LastHasResult,       // 0.0 정상 결과 구분
                            LastSkipReason = meas.LastSkipReason      // null or "DATUM_FAIL"
                        };

                        // DualImage 측정이면 가로축/세로축 2장 경로 기록 (리뷰어 전환 버튼용).
                        // 가로축은 측정에 명시 경로 없으면 Shot 이미지로 fallback.
                        var dualMeas = meas as DualImageEdgeDistanceMeasurement;
                        if (dualMeas != null)
                        {
                            measDto.IsDualImage = true;
                            measDto.HorizontalImagePath = !string.IsNullOrEmpty(dualMeas.TeachingImagePath_Horizontal)
                                ? dualMeas.TeachingImagePath_Horizontal
                                : shot.GetLatestImagePath();
                            measDto.VerticalImagePath = dualMeas.TeachingImagePath_Vertical ?? "";
                        }

                        faiDto.Measurements.Add(measDto);
                    }

                    shotDto.FAIs.Add(faiDto);
                }

                dto.Shots.Add(shotDto);
            }

            return dto;
        }

        /// <summary>
        /// CycleResultDto 를 비동기로 cycle 폴더에 JSON 파일로 저장한다.
        /// 저장 실패 시 Logging 에 에러만 기록하고 호출부(AddResponse) 를 차단하지 않는다.
        /// </summary>
        public static void SaveAsync(CycleResultDto dto)
        {
            if (dto == null)
            {
                return;
            }

            // CycleFolderPath 계산: ResultSavePath + yyyyMMdd + HHmmss_cycle
            // 외부 입력 없음 — ResultSavePath + DateTime 포맷팅만 사용, path traversal 불가
            string dateDir = Path.Combine(
                SystemHandler.Handle.Setting.ResultSavePath,
                dto.InspectionTime.ToString("yyyyMMdd"));
            string cycleDir = Path.Combine(
                dateDir,
                dto.InspectionTime.ToString("HHmmss") + "_cycle");

            dto.CycleFolderPath = cycleDir;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    Directory.CreateDirectory(cycleDir);
                    string jsonPath = Path.Combine(cycleDir, "cycle.json");
                    string json = JsonConvert.SerializeObject(dto, Formatting.Indented);
                    File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    try
                    {
                        Logging.PrintErrLog(
                            (int)ELogType.Error,
                            "[CycleResultSerializer] Save failed: " + ex.Message);
                    }
                    catch { }
                }
            });
        }

        /// <summary>
        /// cycle.json 파일을 역직렬화하여 CycleResultDto 를 반환한다.
        /// 파일 미존재 / 손상 / 악성 JSON → null 반환 (RCE 방지).
        /// TypeNameHandling.None 명시 — 절대 None 외 설정 금지.
        /// </summary>
        public static CycleResultDto Load(string jsonPath)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
                {
                    return null;
                }

                string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                // TypeNameHandling.None 명시 — 손상/악성 JSON → null, unhandled exception 0 (RCE 방지)
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
                return JsonConvert.DeserializeObject<CycleResultDto>(json, settings);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// EVisionResultType → OverallJudgement 문자열 매핑.
        /// 3-state hierarchy 와 일치:
        ///   NotExist → "DETECT_FAIL" / NG → "NG" / 그 외 → "OK"
        /// </summary>
        private static string MapJudgement(EVisionResultType result)
        {
            if (result == EVisionResultType.NotExist)
            {
                return "DETECT_FAIL";
            }

            if (result == EVisionResultType.NG)
            {
                return "NG";
            }

            return "OK";
        }
    }
}
