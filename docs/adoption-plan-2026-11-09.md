# Adoption Plan

> **Generated**: 2026-11-09
> **Project phase**: Production (inferred: 56 C# source files under `Assets/Scripts`, no design artifacts)
> **Engine**: Unity 6000.3.13f1 (Unity 6.3 LTS, URP 17.3)
> **Template version**: v1.0+ (`project.yaml` present, v1.1 config — no v1.0 migration needed)
> **Workflow tier**: minimal (design artifact = `design/game-brief.md`)

Work through these steps in order. Check off each item as you complete it.
Re-run `/adopt` anytime to check remaining gaps.

---

## Step 1: Fix Blocking Gaps

None. Engine, language, rendering and physics are configured in both `project.yaml`
and `.claude/docs/technical-preferences.md`; `docs/engine-reference/unity/VERSION.md`
pins Unity 6.3 LTS and records the installed editor.

---

## Step 2: Fix High-Priority Gaps

### 2a. No game brief

**Problem**: The game was built code-first. There is no `design/game-brief.md`, so every
story/design skill has nothing to trace work back to.
**Fix**: Reverse-document the brief from the implementation (`/reverse-document`), then have
creative-director + game-designer mark the design gaps (onboarding, mode rules, balance numbers,
bot difficulty).
**Time**: 1 session
- [ ] `design/game-brief.md` written (core loop, modes, MVP, build order)

---

## Step 3: Bootstrap Infrastructure

Not on the minimal path — the brief's build order is the plan. Next: `/create-stories`
(no stories exist yet).

---

## Step 4: Medium-Priority Gaps

### 4a. No automated tests

**Problem**: 0 test files and no assembly definitions; `commands.test` will report
`testcasecount="0"`, which is not a pass. All game code lives in `Assembly-CSharp`, which a
test assembly cannot reference.
**Fix**: Move `Assets/Scripts` into a runtime asmdef (+ an Editor asmdef for
`Scripts/Editor`), add `Assets/Tests/EditMode` with regression tests for the three fixed
critical bugs (DOTween recycling, first-turn start, End-Turn soft-lock). `/test-setup` scaffolds
the runner.
**Time**: 1 session
- [ ] Runtime/Editor asmdefs compile
- [ ] EditMode tests run via `commands.test` with `testcasecount > 0`

---

## Step 5: Optional Improvements

### 5a. Forbidden Patterns / Allowed Libraries empty

**Problem**: `.claude/docs/technical-preferences.md` has no project rules yet (empty by design).
**Fix**: Fill as decisions are made — e.g. forbid `DOTween.Init(recycleAllByDefault: true)`
(it caused the stuck-turn bug); list DOTween, FishNet, TextMeshPro as allowed.
**Time**: 5 min
- [ ] Rules recorded

---

## What to Expect from Existing Stories

No stories exist yet. Stories created from here on use the template format.

---

## Re-run

Run `/adopt` again after Step 2 to verify the high-priority gap is resolved.
