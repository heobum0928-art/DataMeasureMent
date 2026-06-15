---
phase: 43-startup-delay-separation
reviewed: 2026-06-15T00:00:00Z
depth: standard
files_reviewed: 4
files_reviewed_list:
  - WPF_Example/Login/LoginManager.cs
  - WPF_Example/SystemHandler.cs
  - WPF_Example/UI/Login/LoginWindow.xaml.cs
  - WPF_Example/Custom/SystemHandler.cs
findings:
  critical: 1
  warning: 2
  info: 2
  total: 5
status: resolved
---

# Phase 43: Code Review Report

**Reviewed:** 2026-06-15
**Depth:** standard
**Files Reviewed:** 4
**Status:** issues_found

## Summary

Phase 43 introduces three tightly scoped changes: (1) `LoginManager` background preload infrastructure (`Preload` / `PreloadWorker` / `EnsureLoaded` / `_isPreloaded`), (2) `SystemHandler.Initialize()` Step 5 switched from synchronous `Load()` to background thread kick-off plus a `[STARTUP] READY` marker after Step 7, and (3) `LoginWindow` constructor calls `EnsureLoaded()` before `GetIDList()`.

The concurrency design is structurally sound for the normal code path. The `volatile bool _isPreloaded` + `Thread.Join()` combination provides adequate visibility fencing on x86/x64 for the login UI scenario. The `MainRun` `Login.IsLogin` read is safe (default `false`, no NRE introduced by this phase; the pre-existing Step 4-before-Step-5 window is unchanged and out of scope). The `Btn_edit_Click` exclusion from `EnsureLoaded` is correctly justified in plan comments.

One critical defect exists: `PreloadWorker` calls `Load()` with no exception guard, and `Load()` has an unguarded `JsonConvert.DeserializeObject` call. A corrupted `account.db` will produce an unhandled background-thread exception, which terminates the process under .NET 4.0+ default behavior — exactly the scenario the sync fallback in `EnsureLoaded` was designed to survive, but cannot, because the process is already dead.

Two warnings cover (a) the non-atomic double-read guard in `Preload()` that would throw `ThreadStateException` if ever called from two threads concurrently, and (b) a memory-model correctness gap on the fast path of `EnsureLoaded` where a non-volatile `AccountList` backing field is relied upon to be visible after a `volatile bool` read.

---

## Critical Issues

### CR-01: `PreloadWorker` wraps `Load()` with no exception handler — corrupted `account.db` terminates the process

**File:** `WPF_Example/Login/LoginManager.cs:177-184`

**Issue:** `PreloadWorker()` calls `Load()` without a try/catch. Inside `Load()`, `Decrypt()` and `JsonConvert.DeserializeObject()` (line 234) are also uncaught. If `account.db` contains truncated ciphertext, a wrong AES padding produces a `CryptographicException`; malformed JSON produces a `JsonException`. Both propagate out of `PreloadWorker` as an unhandled exception on a background thread. Under .NET 4.0+ default policy (`ThrowUnhandledException`) this terminates the process immediately. `_isPreloaded` is never set, and the `EnsureLoaded()` sync-fallback path (the designed recovery) is never reached. The previous synchronous path in the constructor would have crashed the UI thread at a well-defined point; the background path turns a deterministic startup failure into a non-deterministic process kill.

**Fix:**

```csharp
private void PreloadWorker() {
    //260615 hbk Phase 43: Load() 본문 무수정 — 백그라운드 thread 에서 호출만 함
    try {
        if (!Load()) {
            //occurs error or nothing (Load() 내부에서 기본 admin 추가)
        }
    }
    catch (Exception ex) {
        //260615 hbk Phase 43: 백그라운드 thread 미처리 예외 방지 — account.db 손상 시 기본 admin 보장
        Logging.PrintLog((int)ELogType.Error, "[LOGIN] PreloadWorker exception: {0}", ex.Message);
        if (CountOf(EAccountGrade.Admin) == 0) {
            AccountList.Add(new AccountInfo(DEFAULT_ADMIN_ID, EAccountGrade.Admin, DEFAULT_ADMIN_PASSWORD));
        }
    }
    finally {
        _isPreloaded = true; //260615 hbk Phase 43: D-04 완료 신호 — 예외 경로에서도 반드시 세팅
        Logging.PrintLog((int)ELogType.Trace, "[LOGIN] Preload complete: {0} accounts", AccountList.Count);
    }
}
```

Moving `_isPreloaded = true` into `finally` also ensures the flag is always set regardless of whether `Load()` succeeded or threw, which unblocks `EnsureLoaded()`'s Join path and prevents it from hanging if an exception path fires.

---

## Warnings

### WR-01: `Preload()` start guard is not atomic — concurrent calls produce `ThreadStateException`

**File:** `WPF_Example/Login/LoginManager.cs:171-175`

**Issue:** `Preload()` reads `!_isPreloaded` and `!_preloadThread.IsAlive` as two separate non-atomic expressions before calling `_preloadThread.Start()`. Under the documented single-caller contract (only `SystemHandler.Initialize()` Step 5 calls it once) this is safe. However, `Preload()` is `public`, which means nothing prevents a second call from an error-recovery path or a future extension. If two threads both evaluate `_isPreloaded = false` and `IsAlive = false` before either starts the thread, both reach `_preloadThread.Start()`. The second call throws `ThreadStateException` (a thread cannot be started twice). The exception propagates to the caller with no catch site, potentially crashing Initialize().

**Fix — Option A (minimal): Document the single-call contract with a guard that prevents double-Start from throwing**

```csharp
//260615 hbk Phase 43: D-03 — Initialize() Step 5 에서 1회 호출. 이중 guard 로 재기동 방지.
public void Preload() {
    if (_isPreloaded || _preloadThread.IsAlive) return; //260615 hbk Phase 43
    try {
        _preloadThread.Start();
    }
    catch (ThreadStateException) {
        //이미 기동됨 — 정상, 무시
    }
}
```

**Fix — Option B (robust): use `Interlocked` to make the start atomic**

```csharp
private int _preloadStarted; // 0 = not started, 1 = started

public void Preload() {
    if (Interlocked.CompareExchange(ref _preloadStarted, 1, 0) == 0) {
        _preloadThread.Start();
    }
}
```

Option A is lower-friction for the current single-caller constraint; Option B eliminates the race entirely.

---

### WR-02: `AccountList` reference replacement on background thread relies on `volatile bool` to carry visibility of a non-volatile field write

**File:** `WPF_Example/Login/LoginManager.cs:182-188` (EnsureLoaded fast path) and `LoginManager.cs:234` (Load)

**Issue:** `Load()` line 234 executes `AccountList = (ObservableCollection<AccountInfo>)JsonConvert.DeserializeObject(...)`, replacing the `AccountList` backing field (a non-volatile reference) on the background thread. `EnsureLoaded()` fast-path (line 188: `if (_isPreloaded) return;`) reads the `volatile bool _isPreloaded` and then immediately returns. The subsequent `GetIDList()` call reads `AccountList.Count` and `AccountList[i]`. The ECMA CLI memory model does not guarantee that a `volatile` read of `_isPreloaded` acts as a LoadLoad fence for the subsequent read of the unrelated non-volatile `AccountList` backing field. In principle, the JIT is allowed to hoist the `AccountList` field read above the `_isPreloaded` volatile read. On x86/x64 (TSO) this reordering does not occur in hardware, so this is not a practical bug today; but it is a correctness gap against the CLI specification.

**Fix:** Declare the `AccountList` property backing with an explicit `volatile` field, or use `Thread.MemoryBarrier()` before reading `AccountList` in `GetIDList()`. The lowest-friction fix is a barrier in `EnsureLoaded()` after the fast-path return check:

```csharp
public void EnsureLoaded() {
    if (_isPreloaded) {
        Thread.MemoryBarrier(); //260615 hbk: ensure AccountList write visible after _isPreloaded read
        return;
    }
    if (_preloadThread.IsAlive) {
        _preloadThread.Join(); // Join() provides full fence — no barrier needed after
    }
    else if (!_isPreloaded) {
        Load();
        _isPreloaded = true;
    }
}
```

Alternatively, the `AccountList` field can be changed to an explicit `volatile` field (requires converting the auto-property to a field-backed property).

---

## Info

### IN-01: `LoginManager.Handle.Preload()` in `SystemHandler.Initialize()` duplicates a Handle access already cached in `Login`

**File:** `WPF_Example/SystemHandler.cs:151-152`

**Issue:** Step 5 assigns `Login = LoginManager.Handle` then calls `LoginManager.Handle.Preload()` on a second access to the `Handle` static getter. The second access is redundant; `Login.Preload()` is equivalent and makes the intent clearer (using the already-resolved reference).

**Fix:**

```csharp
Login = LoginManager.Handle;
Login.Preload(); //260615 hbk Phase 43: 백그라운드 Thread 기동
```

---

### IN-02: `PreloadWorker` empty-comment block around the `Load()` return value reduces signal

**File:** `WPF_Example/Login/LoginManager.cs:179-181`

**Issue:** The block `if (!Load()) { //occurs error or nothing (Load() 내부에서 기본 admin 추가) }` conveys useful intent but becomes noise once CR-01 is fixed with a proper try/catch. After the fix the pattern becomes redundant because both the success and failure paths are handled in the catch/finally. No action needed until CR-01 is addressed; noting here so the comment is cleaned up alongside the CR-01 fix.

**Fix:** After applying CR-01 fix, the `if (!Load())` check and its comment block can be collapsed into just `Load();` inside the try body.

---

_Reviewed: 2026-06-15_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

---

## Fix Resolution (2026-06-15)

CR-01, WR-01, WR-02 fixed in commit `491d5b0`
(`fix(43-01): harden LoginManager bg preload — crash-safe PreloadWorker + atomic Preload start + memory barrier (CR-01/WR-01/WR-02)`)

- **CR-01**: `PreloadWorker` wrapped in `try/catch(Exception)/finally` — unhandled bg-thread exception absorbed, default admin guaranteed on corruption, `_isPreloaded = true` moved to `finally` to always signal completion.
- **WR-01**: `Preload()` wraps `_preloadThread.Start()` in `try/catch(ThreadStateException)` — concurrent double-call defended without crashing caller.
- **WR-02**: `EnsureLoaded()` fast-path inserts `System.Threading.Thread.MemoryBarrier()` after `volatile _isPreloaded` read — non-volatile `AccountList` field visibility correctly fenced per CLI spec.

IN-01, IN-02: deferred (info-only, no behavioral impact).
