# Technical Preferences

<!-- project.yaml at the repo root is the machine-readable source of truth for
     engine, specialists, naming, performance, platform, and testing.framework.
     This file is the human-readable LEGACY FALLBACK: agents and skills resolve
     each key from project.yaml first and fall back here only when the
     project.yaml key is absent. /setup-engine dual-writes both.
     Forbidden patterns and allowed libraries are NOT migrated — they live only
     in this file. Populated by /setup-engine; updated as decisions are made. -->

## Engine & Language

- **Engine**: Unity 6000.3.13f1 (Unity 6.3 LTS)
- **Language**: C#
- **Rendering**: URP
- **Physics**: PhysX

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Windows 64), Mobile (Android)
- **Input Methods**: Keyboard/Mouse, Touch, Gamepad (partial)
- **Primary Input**: Mouse / Touch (turn-based board game: tap/click tiles and buttons)
- **Gamepad Support**: Partial
- **Touch Support**: Full
- **Platform Notes**: No hover-only information (touch has no hover). Respect safe areas on mobile. Unity Input System package.

## Naming Conventions

- **Classes**: PascalCase (e.g., `PlayerController`)
- **Variables**: public PascalCase properties; private fields `_camelCase` (e.g., `_moveSpeed`)
- **Signals/Events**: C# events `On<Event>` (e.g., `OnTurnStarted`)
- **Files**: PascalCase matching class (e.g., `PlayerController.cs`)
- **Scenes/Prefabs**: PascalCase
- **Constants**: PascalCase or UPPER_SNAKE_CASE

## Performance Budgets

- **Target Framerate**: 60 fps
- **Frame Budget**: 16.6 ms
- **Draw Calls**: <= 500 (PC), <= 200 (mobile)
- **Memory Ceiling**: [TO BE CONFIGURED — set once mobile target devices are known]

## Testing

- **Framework**: NUnit via Unity Test Framework (EditMode + PlayMode)
- **Minimum Coverage**: [TO BE CONFIGURED]
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- [None configured yet — add as dependencies are approved]

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /studio-code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP/HDRP materials)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: unity-dots-specialist (ECS, Jobs system, Burst compiler), unity-addressables-specialist (asset loading, memory management, content catalogs)
- **Routing Notes**: Invoke primary for architecture and general C# code review. Invoke DOTS specialist for any ECS/Jobs/Burst code. Invoke shader specialist for rendering and visual effects. Invoke UI specialist for all interface implementation. Invoke Addressables specialist for asset management systems.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | Primary |
