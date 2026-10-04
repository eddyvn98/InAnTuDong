# AGENTS.md

This file prevents project drift between AI/chat sessions.

## Before changing code

1. Read `docs/PROGRESS.md`.
2. Read `docs/ROADMAP.md`.
3. Read the relevant specifications under `docs/`.
4. Inspect existing code before proposing a rewrite.
5. Continue the current milestone unless a documented decision changes direction.

## Current UI source of truth

Before making any claim about the current user-facing UI, inspect the host routing and the actual surface being used.

- The current AI-first local browser UI is `src/PrintAI.Web/wwwroot/local.html` plus the `local*.js` modules and shared `site.css`.
- Its intended primary surface is deliberately minimal: source selection/thumbnails, natural-language request input, document/print preview, and print controls. Do not infer extra user-facing complexity from internal capabilities.
- `src/PrintAI.Desktop/ui/index.html` is the older WPF/WebView2 desktop surface. Do not use it as evidence of the current AI-first UI unless the task explicitly targets that legacy desktop surface.
- `src/PrintAI.Web/wwwroot/index.html` is the hosted Railway demo surface. When the hosting platform sets `PORT`, `PrintAI.Web` intentionally serves this demo instead of the local workflow. Do not treat the Railway demo as the canonical local-product UI.
- Check `src/PrintAI.Web/Program.cs` before deciding which web surface a runtime will serve.
- The repository source currently wins over screenshots, old branches, and stale chat descriptions when identifying the active UI.

## Before ending a session

1. Update `docs/PROGRESS.md` with what is actually complete.
2. Record blockers and the exact next task.
3. Put architecture/product decisions in `docs/DECISIONS.md`.
4. Do not mark work complete without tests, CI, or manual verification as appropriate.
5. For General Print Intent changes, read `docs/GENERAL_PRINT_INTENT.md` before editing planner/domain behavior.

## Engineering rules

- Prefer mature libraries/platform APIs over hand-written commodity infrastructure.
- Keep physical print geometry deterministic.
- AI may produce `PrintJobSpec 1.0` for simple uniform jobs or `PrintPlan 2.0` for multi-rule print intent; AI may not directly issue arbitrary print commands.
- Keep the product focused on document/image preparation plus printing. Editing, reformatting, arranging and generating user-selected artifacts are in scope; CRM, pricing, payment, inventory, delivery and unrelated order-management remain out of scope.
- Store physical dimensions in millimetres.
- A4 is the maximum paper size for the first printer profile.
- Epson L3310 is a device profile, not a domain dependency.
- Unknown/risky jobs require preview or clarification.
- Avoid Epson UI automation except as a documented fallback.
- Keep source files focused and reasonably small.
- New meaningful dependencies must be documented in `docs/LIBRARIES.md`.
- Railway remains test/demo only. Windows remains the production scanner target. The cross-platform `PrintAI.Web` host supports macOS local upload, preview, PDF export, and direct CUPS printer submission through a dedicated adapter; real-device print fidelity still requires validation on a configured Mac printer.
- Natural-language planning uses the installed Antigravity CLI by default. Do not add a hidden AI API-key dependency.
- Antigravity may execute artifact preparation only inside a per-task isolated workspace containing copies of user-selected files. Final artifact files are re-imported into PrintAI before preview/printing. Deterministic PrintAI code still owns print validation, geometry, preview, policy and spooler execution, and AGY must never call printer/spooler commands directly.

## Definition of done

A milestone is done only when acceptance criteria are met, tests are green where practical, progress docs are current, and the next concrete task is recorded.
