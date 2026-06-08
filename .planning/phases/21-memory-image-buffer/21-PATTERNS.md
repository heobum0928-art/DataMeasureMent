# Phase 21: Memory Image Buffer — Pattern Map

**Mapped:** 2026-05-10
**Files analyzed:** 7 (5 source modifications + 1 doc-only reference + 1 lookup helper)
**Analogs found:** 7 / 7 (all in-tree, no external analog needed)
**Search scope:** `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/Sequence/`, `WPF_Example/Custom/`, `WPF_Example/UI/`, `WPF_Example/MainWindow.xaml.cs`

---

## File Classification

| File (modified/referenced) | Role | Data Flow | Closest Analog | Match Quality | Change Type |
|----------------------------|------|-----------|----------------|---------------|-------------|
| `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` | model (param) | thread-safe in-memory buffer | self (XML doc add only) | exact (self) | XML doc + comment |
| `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` | service (recipe) | CRUD + lifetime owner | self + `ShotConfig.ClearImage` | exact (self) | XML doc only |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | action (sequence step) | request-response (per Run cycle) | self (intent comment) | exact (self) | comment marker only |
| `WPF_Example/Custom/SystemHandler.cs` | controller (lifecycle) | event-driven (TCP packet dispatch + subscriber wire-up) | `MainWindow.xaml.cs:82` (`Sequences.OnRecipeChanged += OnLoadRecipe`) | exact | subscriber add (D-02 channel #1 + #3) |
| `WPF_Example/SystemHandler.cs` | controller (lifecycle) | request-response (Initialize/Release) | self `Initialize` step list + `Release()` dispose chain | exact (self) | `Release()` ClearShots verify/add |
| `WPF_Example/Sequence/SequenceHandler.cs` | service (recipe + sequence) | event-driven (publisher) | self (no change — read-only ref) | n/a | NO CHANGE — publisher only |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | UI (code-behind) | request-response (FAI tree click) | self (DisplayShotImage L113-132) | n/a | NO CHANGE — AC#1 verification ref |

**Key observations from CONTEXT.md cross-check:**

- `ShotConfig.cs` was **NOT** touched by Phase 20 (still uses `_image?.Dispose()` at L61, K&R brace style). Phase 21 should preserve K&R + `?.` for unchanged dispose lines and apply Phase 20 D-01 (explicit if/else) only on **newly added** lines.
- `OnRecipeChanged?.Invoke(...)` at `SequenceHandler.cs:162` was **NOT** touched by Phase 20 either (still uses `?.` on event invocation — convention C# safe pattern, was likely Phase 20 D-04/D-05 LINQ-tail-equivalent exception). Phase 21 must NOT modify the publisher line.
- `RecipeManager` is exposed as `public` on the **partial** `SequenceHandler` (Custom side, L24) — `SystemHandler.Handle.Sequences.RecipeManager` is the canonical access path (used in `InspectionListView.xaml.cs:167`, `InspectionSequence.cs:69`).
- `ClearShots()` is **only** called from inside `LoadPhase6Format()` at `InspectionRecipeManager.cs:166` today. **App shutdown gap CONFIRMED** — `SystemHandler.Release()` (L163-193) does NOT call it.

---

## Pattern Assignments

### `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` (model, thread-safe in-memory buffer)

**Analog:** self (existing thread-safe HImage buffer pattern is already correct — Phase 21 just documents the contract).

**Existing thread-safe buffer pattern** (L43-77, **DO NOT change behavior**, only add `///` XML doc + Phase 21 marker):

```csharp
        // Thread-safe image buffer
        private readonly object _imageLock = new object();
        private HImage _image;

        [Browsable(false)]
        public bool HasImage {
            get { lock (_imageLock) { return _image != null; } }
        }

        public ShotConfig(object owner) : base(owner) {
        }

        public ShotConfig(object owner, string name) : base(owner) {
            ShotName = name;
        }

        public void SetImage(HImage image) {
            lock (_imageLock) {
                _image?.Dispose();
                _image = image?.CopyImage();
            }
        }

        public HImage GetImage() {
            lock (_imageLock) {
                return _image?.CopyImage();
            }
        }

        public void ClearImage() {
            lock (_imageLock) {
                _image?.Dispose();
                _image = null;
            }
        }
```

**XML doc style — analog reference** (already in this file at L99-100 of `MainView.xaml.cs`):

```csharp
        /// <summary>Displays the shot image associated with the selected FAIConfig. Per D-12.
        /// FAIConfig itself does not store an image; the parent ShotConfig holds it.</summary>
        public void DisplayFAIImage(FAIConfig fai) {
```

**Brace style for this file:** **K&R** (opening brace on same line — see L9, L48, L52, L59, L66, L72). Per CLAUDE.md "Use the style of the file/module you are editing." Phase 21 XML doc additions are above the method signature → no brace style impact.

**Phase 21 hbk marker rule:** Per CONTEXT.md `<specifics>`, every new/modified line gets `//260510 hbk Phase 21`. Per Phase 20 D-12 (referenced in 21-CONTEXT.md), markers do NOT stack — XML-doc-only changes use a single Phase 21 marker on the doc block (or omit if convention permits XML doc as "doc-only"). **Planner decides** marker placement (recommendation: place a single `//260510 hbk Phase 21: lifetime contract documented` marker as a regular comment line just above the XML doc block per method, since XML `///` lines themselves are syntactically distinct).

**XML doc targets** (per D-06):

| Member | Doc must specify |
|--------|------------------|
| `SetImage(HImage)` (L59) | clone-on-input + auto-dispose existing `_image`; **caller retains ownership of input** and must dispose it |
| `GetImage()` (L66) | clone-on-output; **caller MUST dispose** the returned HImage (using-block pattern enforced) |
| `ClearImage()` (L72) | **lifetime termination**; called at: (1) recipe change via `OnRecipeChanged` subscriber, (2) sequence reset via `Action_FAIMeasurement.EStep.Init` → `ClearAllResults`, (3) app shutdown via `SystemHandler.Release()` |
| `HasImage` (L48) | `_imageLock` synchronized — safe to call from any thread |
| `ClearAllResults()` (L96) | sequence reset trigger — disposes `_image` and clears each FAI result; called from `Action_FAIMeasurement.EStep.Init` (L60) every Run cycle entry |

---

### `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` (service, lifetime owner)

**Analog:** self.

**Existing pattern** (L51-56) — **NO behavioral change**, only XML doc add:

```csharp
        public void ClearShots() {
            foreach (var shot in Shots) {
                shot.ClearImage();
            }
            Shots.Clear();
        }
```

**Existing call sites** (Grep evidence):
- `InspectionRecipeManager.cs:166` — `LoadPhase6Format()` calls `ClearShots()` first (recipe reload path).
- `RemoveShot()` (L46) — calls `Shots[index].ClearImage()` per-shot (different scope, not full clear).

**XML doc target** (per D-06):

```csharp
        /// <summary>
        /// Disposes every Shot's HImage buffer and empties the Shot list.
        /// Lifetime contract (Phase 21 BUF-02): MUST be invoked on
        ///   (1) recipe change — wired from SequenceHandler.OnRecipeChanged subscriber,
        ///   (2) app shutdown — invoked from SystemHandler.Release().
        /// Idempotent: safe to call multiple times (ClearImage is null-safe under lock).
        /// </summary>
        public void ClearShots() {
```

**Brace style:** K&R (file-wide — see L13, L51, L92).
**Marker:** `//260510 hbk Phase 21` on the new XML doc block.

---

### `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (action, sequence step)

**Analog:** self (intent marking only, **no behavioral change**).

**Existing pattern** (L57-62 — `EStep.Init` clears the buffer at the start of every Run cycle):

```csharp
        public override ActionContext Run() {
            switch ((EStep)Step) {
                case EStep.Init:
                    ShotParam?.ClearAllResults();
                    Step = (int)EStep.MoveZ;
                    break;
```

**Phase 21 change** (D-02 channel #2 — sequence reset marker):

Add a `//260510 hbk Phase 21: BUF-02 sequence reset trigger — disposes shot HImage buffer per Run cycle entry` comment immediately above L60. Do NOT modify the line itself.

**Why marker, not change:** `ClearAllResults()` is already the de-facto sequence reset hook (D-04 explicitly rejected adding `SequenceBase.OnReset`). Phase 21's contribution is making the intent **discoverable** in code-grep (so a future maintainer searching for "buffer dispose" finds this site).

**Brace style:** K&R (file-wide — see L27, L57). **Existing `?.` operators in this file (L51 `ShotParam != null`, L60 `ShotParam?.ClearAllResults()`) were NOT touched by Phase 20** — preserve as-is. Phase 21 adds NO new operators here.

---

### `WPF_Example/Custom/SystemHandler.cs` (controller, event subscriber wire-up)

**Analog (subscriber registration pattern):** `WPF_Example/MainWindow.xaml.cs:82` — single-line subscribe pattern.

**Excerpt — MainWindow subscriber pattern** (L82, L84):

```csharp
            mSystemHandler.Sequences.OnRecipeChanged += this.OnLoadRecipe;

            mSystemHandler.Login.OnLoginStateChanged += this.OnLoginChanged;
```

**Excerpt — MainWindow handler signature** (L236-239):

```csharp
        public void OnLoadRecipe(object sender, RecipeChangedEventArgs args) {
            //args.RecipeName;
            inspectionList.OnLoadRecipe(args.RecipeName);
        }
```

**Excerpt — MainWindow unsubscribe pattern at shutdown** (L360-367):

```csharp
            for (int i = 0; i < mSystemHandler.Sequences.Count; i++) {
                mSystemHandler.Sequences[i].OnStart -= OnSequenceStart;
                mSystemHandler.Sequences[i].OnStop -= OnSequenceStop;
                mSystemHandler.Sequences[i].OnError -= OnSequenceError;
                mSystemHandler.Sequences[i].OnFinish -= OnSequenceFinish;
                //260409 hbk Phase 5: Shot별 실시간 UI 갱신 해제 (D-12)
                mSystemHandler.Sequences[i].OnActionChanged -= OnActionChanged;
            }
            mSystemHandler.Release();
```

**Note:** `MainWindow.Window_Closing` unsubscribes per-sequence events but does **NOT** unsubscribe `OnRecipeChanged`. This is the **existing convention** for handler-events that share singleton lifetime — there is no leak because both `MainWindow` and `SequenceHandler` (singleton) live until process exit. Phase 21 subscriber MAY follow the same convention or add explicit unsubscribe (per D-04 Claude's Discretion: "subscriber 등록/해제 lifecycle 보호 — planner 결정").

**RecipeManager access path** (analog from `InspectionListView.xaml.cs:167`):

```csharp
                var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
```

**Phase 21 wire-up location decision matrix** (per D-03 — planner picks one):

| Candidate location | Pros | Cons |
|--------------------|------|------|
| **`Custom/SystemHandler.cs` Initialize()** (recommended per D-03) | App-level lifecycle (correct ownership); `Sequences` already initialized at L112 of framework `SystemHandler.Initialize()`; partial-class extension natural | Custom partial currently has no `Initialize()` override — needs new method or inline in framework `Initialize()` |
| `MainWindow.xaml.cs:82` (next to `OnLoadRecipe +=`) | Closest analog; trivial code change | Wrong layer — UI code subscribing to model lifecycle event for non-UI purpose; couples buffer lifetime to UI window |
| `Custom/Sequence/SequenceHandler.cs` (partial side) | Closest to event publisher | `OnRecipeChanged` is in framework partial, RecipeManager is in custom partial — same class but separate files; introduces self-referential subscribe |

**Recommended location:** Per D-03 → `Custom/SystemHandler.cs`. Since framework `SystemHandler.Initialize()` (L103-146) sets `Sequences = SequenceHandler.Handle` at L112, the subscriber wire-up should happen **after** L112 but **before** `Logging.PrintLog((int)ELogType.Trace, "[SYSTEM] Initialized");` at L145. The partial Custom file currently has no `Initialize()` override. **Planner must decide:** (a) add a new private `WireBufferLifecycle()` method on the Custom partial called from framework `Initialize()`, or (b) add subscriber inline in framework `Initialize()` itself (changes to `WPF_Example/SystemHandler.cs` instead of `Custom/SystemHandler.cs`).

**Recommended subscriber pattern** (single-line, follows `MainWindow.xaml.cs:82` analog, K&R-friendly):

```csharp
        // Event handler signature mirrors MainWindow.OnLoadRecipe (L236)
        private void OnRecipeChanged_FlushBuffers(object sender, RecipeChangedEventArgs args) {
            Sequences.RecipeManager.ClearShots();  //260510 hbk Phase 21 BUF-02 channel #1
        }
```

**Brace style for `Custom/SystemHandler.cs`:** K&R (see file-wide — L14, L16, L75, L156). Match.

**Phase 20 D-01 explicit-if/else policy:** Subscriber method body has no operators to convert. New code is plain method call. ✅ no policy violation.

---

### `WPF_Example/SystemHandler.cs` (controller, Release lifecycle)

**Analog:** self — `Release()` already follows a deterministic dispose chain (L163-193). Phase 21 inserts ONE call into the existing chain.

**Existing `Release()` dispose chain** (L163-193):

```csharp
        public void Release() {
            // Persist settings.
            Setting.Save();

            // Release device resources.
            Devices.Dispose();

            // Release sequences.
            Sequences.Dispose();

            //260317 raw image save worker for inspection flow
            RawImageSaver?.Dispose();
            RawImageSaver = null;

            // Stop TCP server.
            Server.Dispose();

            // Release light controller.
            Lights.Release();

            // Stop system thread.
            // Join timeout prevents UI lockup on shutdown.
            IsTerminated = true;
            mSystemThread.Join(1000);

            Logging.PrintLog((int)ELogType.Trace, "[SYSTEM] Released");

            // Stop logging system last.
            Logging.Stop();
            IsReleased + true;
        }
```

**Phase 21 insertion point analysis** (D-02 channel #3):

`Sequences.Dispose()` at L171 calls `SequenceHandler.Dispose()` which calls `seq.Release()` for each sequence — this does **NOT** invoke `RecipeManager.ClearShots()` today (verified via Grep — `ClearShots` is only called in `LoadPhase6Format()`).

**Recommended insertion:** **before** `Sequences.Dispose()` at L171 (so RecipeManager state is alive when ClearShots iterates):

```csharp
            // Release sequences.
            Sequences.RecipeManager.ClearShots();  //260510 hbk Phase 21 BUF-02 channel #3 (app shutdown)
            Sequences.Dispose();
```

**Alternative (planner choice per D-03 freedom):** Place inside `SequenceHandler.Dispose()` itself (L57-63 of framework `SequenceHandler.cs`). Trade-off: framework file (vs custom) — keeps shutdown invariant on the publisher side, but `RecipeManager` is on the Custom partial → cross-partial reference is fine since same class.

**Brace style:** K&R (file-wide — L19, L67, L103, L163). Match.

**Phase 20 D-01 policy:** New line is plain method call sequence. ✅ no operator conversion needed.

---

### `WPF_Example/Sequence/SequenceHandler.cs` (publisher — NO CHANGE)

**Reference only.** The event publisher line at L162:

```csharp
            OnRecipeChanged?.Invoke(this, new RecipeChangedEventArgs(name));
```

Phase 21 must NOT modify this line. The `?.Invoke` pattern is the standard C# null-safe event raise idiom (no subscribers ⇒ event is null) — Phase 20 D-04/D-05 explicitly excluded this idiom from operator-conversion (verified by Phase 20 SUMMARY files which list 33 operator sites in MainView.xaml.cs but never touch this file).

---

### `WPF_Example/UI/ContentItem/MainView.xaml.cs` (UI — NO CHANGE)

**Reference only — AC#1 verification target.** The disk-free display path at L113-132:

```csharp
        /// <summary>Displays the image stored in the given ShotConfig on the canvas.</summary>
        private void DisplayShotImage(ShotConfig shot) {
            if (shot != null && shot.HasImage) {
                HImage img = null;
                try {
                    img = shot.GetImage();
                    if (img != null) {
                        halconViewer.LoadImage(img);
                        label_message.Visibility = Visibility.Collapsed;
                    } else {
                        label_message.Content = "이미지 로드 실패";
                        label_message.Visibility = Visibility.Visible;
                    }
                } finally {
                    if (img != null) img.Dispose(); //260509 hbk Phase 20 — ?. expanded
                }
            } else {
                label_message.Content = "NO Image";
                label_message.Visibility = Visibility.Visible;
            }
        }
```

**Why this is the correct AC#1 path:**
- `shot.GetImage()` (L117) reads from the in-memory clone — **no** disk API.
- `halconViewer.LoadImage(img)` (L119) loads from `HImage` instance — **no** `HImage.ReadImage(string)` or `File.*` call.
- `img.Dispose()` honors the GetImage/dispose contract (caller-owns).

**Phase 21 verification approach** (D-08 — planner picks):
- Option A (recommended primary): grep audit for `File.` / `HImage.ReadImage` / `new HImage(<path>)` / `BitmapImage.UriSource` calls reachable from `DisplayFAIImage` → `DisplayShotImage` → `shot.GetImage` chain. Expected count: **0 disk APIs**.
- Option B (recommended secondary): Process Monitor capture during SIMUL_MODE result-image-review click. Expected: **0 file open/read events under recipe image directory** during the click (image grab phase will show I/O — that is acceptable, only the review-click must be disk-free).
- Option C: both (high-confidence).

---

## Shared Patterns

### Pattern: Lifecycle marker comments

**Source:** `Action_FAIMeasurement.cs:64`, `Action_FAIMeasurement.cs:77`, `MainWindow.xaml.cs:91`, `MainWindow.xaml.cs:365` (Phase 5 D-12 marker style).

**Apply to:** All Phase 21 modified files for new/changed lines.

**Convention (from CLAUDE.md feedback_comment_convention + 21-CONTEXT.md `<specifics>`):**

```csharp
//260510 hbk Phase 21: <one-line why>
```

**Phase 20 D-12 stacking rule** (cross-referenced from 21-CONTEXT.md): markers do NOT stack. If a line was previously marked `//260413 hbk Phase 6` and Phase 21 changes the line, replace with `//260510 hbk Phase 21` — preserve "why" content if still relevant.

### Pattern: Single-line event subscribe (publisher already exists)

**Source:** `MainWindow.xaml.cs:82` and L84.

```csharp
            mSystemHandler.Sequences.OnRecipeChanged += this.OnLoadRecipe;
            mSystemHandler.Login.OnLoginStateChanged += this.OnLoginChanged;
```

**Apply to:** Phase 21 buffer-flush subscriber wire-up (D-02 channel #1). Single line, paired with handler method body (3-5 lines).

### Pattern: Singleton access through `SystemHandler.Handle`

**Source:** `InspectionListView.xaml.cs:167`, `InspectionSequence.cs:69`.

```csharp
                var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
```

**Apply to:** Phase 21 documentation examples (XML doc cross-references) and possibly subscriber implementation if external to `SystemHandler` partial (e.g., if planner picks alternative location).

### Pattern: Caller-disposes-clone (HImage lifecycle contract)

**Source:** `Action_FAIMeasurement.cs:141` (the canonical caller pattern that XML doc on `GetImage` must reference):

```csharp
                        using (var image = ShotParam.GetImage()) {
                            if (image != null) {
                                // ... use image ...
                            }
                        }
```

**And** `MainView.xaml.cs:115-127` (try/finally variant — Phase 20 expanded `?.`):

```csharp
                HImage img = null;
                try {
                    img = shot.GetImage();
                    if (img != null) { ... }
                } finally {
                    if (img != null) img.Dispose();
                }
```

**Apply to:** XML doc on `ShotConfig.GetImage()` (D-06) — explicitly cite both patterns as approved consumption styles.

### Pattern: `lock (_imageLock)` — thread-safety boundary

**Source:** `ShotConfig.cs:44, 49, 60, 67, 73`.

**Apply to:** `ShotConfig.HasImage` XML doc must mention `_imageLock` synchronization guarantee (D-06 line 4).

### Pattern: Brace style — K&R (file-wide)

All Phase 21 modified files use **K&R** (`{` on same line as declaration). Per CLAUDE.md "Use the style of the file/module you are editing." Applies to:

- `ShotConfig.cs` — K&R confirmed (L9, L48, L52, L59).
- `InspectionRecipeManager.cs` — K&R confirmed (L13, L51, L92).
- `Action_FAIMeasurement.cs` — K&R confirmed (L27, L57).
- `Custom/SystemHandler.cs` — K&R confirmed (L14, L75).
- `SystemHandler.cs` — K&R confirmed (L19, L67, L163).

### Pattern: Phase 20 D-01 explicit if/else (newly added lines only)

For Phase 21 new code, expand any `?:` / `??` / `?.` to explicit `if`/`else` per Phase 20 D-01 (referenced from 21-CONTEXT.md inherited convention). Existing `?.` lines that Phase 21 does NOT touch remain as-is (Phase 20 D-12: do not touch unmodified lines).

**Concrete impact:** The recommended subscriber implementation (`Sequences.RecipeManager.ClearShots();`) is a plain method call — no operators. ✅ no D-01 friction.

---

## No Analog Found

**None.** Every Phase 21 change has a direct in-tree analog. This phase is intentionally minimal-scope (D-05: zero new classes/interfaces) — all patterns reuse existing code shapes.

---

## Critical Cross-references for Planner

### 1. `ShotConfig.SetImage` current state — Phase 20 D-02 verification

**Status:** Phase 20 did **NOT** modify `ShotConfig.cs`. Verified via Phase 20 plan summaries (only `MainView.xaml.cs` Phase 20 plan touched `?.` patterns adjacent to ShotConfig). The current state at `ShotConfig.cs:60-64` remains:

```csharp
        public void SetImage(HImage image) {
            lock (_imageLock) {
                _image?.Dispose();
                _image = image?.CopyImage();
            }
        }
```

**Phase 21 policy:** Do NOT expand these `?.` operators. They are not "newly added" lines for Phase 21 (Phase 21 only adds XML doc + a marker comment). Phase 20 D-12 stacking rule prevents touching them. They will be addressed by Phase 26 (헝가리안 전체 리팩토링) which will likely also normalize this style.

### 2. `OnRecipeChanged` subscriber location decision (D-03)

**Three candidates** (in order of correctness per pattern-fit):

1. **`Custom/SystemHandler.cs`** — recommended by D-03. Requires planner to decide between:
   - (1a) Add new partial method `WireBufferLifecycle()` invoked from framework `SystemHandler.Initialize()` (preserves separation between framework and custom);
   - (1b) Add inline subscribe in framework `WPF_Example/SystemHandler.cs:Initialize()` after L112 (`Sequences = SequenceHandler.Handle;`) — simpler but blurs framework/custom boundary.

2. **`MainWindow.xaml.cs:82` (next to `OnLoadRecipe +=`)** — analog-cleanest but layering-incorrect. UI window owning model lifecycle event is wrong layer.

3. **`Custom/Sequence/SequenceHandler.cs`** (partial side) — self-referential subscribe within same class. Cleanest publisher-side ownership but violates "subscriber knows publisher" convention used elsewhere in codebase.

**Pattern-fit recommendation:** **(1a)**. Reasoning:
- Existing convention has SystemHandler as the lifecycle owner ("[SYSTEM] Initialized" / "[SYSTEM] Released" log markers anchor lifecycle on SystemHandler).
- Framework `Initialize()` already chains `Sequences.ExecOnCreate()` (L134), `Recipes.CollectRecipe()` (L138) — adding one more cross-cutting wire-up line at L135 is a clean continuation.
- Symmetrical cleanup in `Release()` (D-02 channel #3) keeps both ends of the lifetime contract in the same file.

### 3. App shutdown `ClearShots` gap verification (D-02 channel #3)

**Verified:** `WPF_Example/SystemHandler.cs:163-193` `Release()` does NOT call `RecipeManager.ClearShots()`.
**Verified:** `WPF_Example/Sequence/SequenceHandler.cs:57-63` `Dispose()` only calls `seq.Release()` per sequence — does NOT call `RecipeManager.ClearShots()` either.
**Verified:** `WPF_Example/Custom/Sequence/SequenceHandler.cs:24` `RecipeManager` is `public` instance (not static) — accessible via `SystemHandler.Handle.Sequences.RecipeManager`.

**Action required:** Phase 21 MUST add the call. Recommended location is `WPF_Example/SystemHandler.cs:171` (immediately before `Sequences.Dispose();`).

### 4. AC#1 grep audit scope (D-08)

If planner picks Option A (grep audit), the grep target set is:

| Forbidden API | Pattern | Search scope |
|---------------|---------|--------------|
| HImage file load | `HImage.ReadImage`, `new HImage(<string>)` constructor with path | `MainView.xaml.cs` — `DisplayShotImage`, `DisplayFAIImage` callers |
| .NET file IO | `File.Open`, `File.ReadAllBytes`, `FileStream` | same scope |
| Image source URI | `BitmapImage.UriSource`, `new BitmapImage(new Uri(...))` | same scope |

**Expected:** 0 hits within the call chain. (Hits inside Action_FAIMeasurement.cs Grab step at L110 `new HImage(ShotParam.SimulImagePath)` are **outside AC#1 scope** — that is the inspection-time grab, not the review-time display).

---

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/` — buffer ownership (3 files)
- `WPF_Example/Sequence/` — publisher event (1 file)
- `WPF_Example/Custom/` — controller wire-up (1 file)
- `WPF_Example/` — root-level controller (1 file: SystemHandler.cs)
- `WPF_Example/UI/` — verification target (1 file: MainView.xaml.cs)
- `WPF_Example/MainWindow.xaml.cs` — subscriber analog (1 file)

**Files scanned (Read tool):** 7 (all canonical refs from CONTEXT.md)
**Grep cross-validations:** OnRecipeChanged subscribers (1 hit: MainWindow.xaml.cs:82), RecipeManager access (3 hits, all via `SystemHandler.Handle.Sequences.RecipeManager`), ClearShots callers (1 hit: InspectionRecipeManager.cs:166 — internal only).

**Pattern extraction date:** 2026-05-10

---

*Phase: 21-memory-image-buffer*
*Patterns mapped: 2026-05-10 — by gsd-pattern-mapper*
