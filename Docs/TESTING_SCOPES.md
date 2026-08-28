# Test scopes

WorldBuilder validation is intentionally opt-in and ownership-scoped. Script compilation does not automatically launch a test suite.

| Scope | Editor command | Request marker | Intended use |
| --- | --- | --- | --- |
| Column Blade | `WorldBuilder/Validate Column Blade Generator` | `Temp/WorldBuilder.RunColumnBladeTests` | Column Blade geometry, materials, lab UI/presentation, and capture tooling |
| Gameplay infrastructure | `WorldBuilder/Validate Gameplay Infrastructure` | `Temp/WorldBuilder.RunInfrastructureTests` | Shared gameplay scene builders and scene contracts; currently the categorized `GameplaySceneInfrastructureTests` fixture |
| Full EditMode | `WorldBuilder/Validate Full EditMode Suite` | `Temp/WorldBuilder.RunFullEditModeTests` | Cross-cutting changes, release checkpoints, validation-framework changes, or explicit requests only |

Choose the smallest applicable scope. A focused feature change normally requires a clean Unity compile, its focused suite, and exercise of the affected scene. It does not require raid, Combat Lab, inventory, or unrelated weapon tests.

Do not delete valuable coverage merely to shorten iteration time. Move tests into an accurate category or add a focused category when a subsystem grows. Keep full-suite execution explicit so unrelated failures do not block local feature work.
