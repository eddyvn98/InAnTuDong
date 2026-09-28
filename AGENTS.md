# AGENTS.md

This file prevents project drift between AI/chat sessions.

## Before changing code

1. Read `docs/PROGRESS.md`.
2. Read `docs/ROADMAP.md`.
3. Read the relevant specifications under `docs/`.
4. Inspect existing code before proposing a rewrite.
5. Continue the current milestone unless a documented decision changes direction.

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
- Keep the product print-only: do not add CRM, pricing, payment, inventory, delivery, or unrelated order-management scope.
- Store physical dimensions in millimetres.
- A4 is the maximum paper size for the first printer profile.
- Epson L3310 is a device profile, not a domain dependency.
- Unknown/risky jobs require preview or clarification.
- Avoid Epson UI automation except as a documented fallback.
- Keep source files focused and reasonably small.
- New meaningful dependencies must be documented in `docs/LIBRARIES.md`.
- Railway remains test/demo only; production printer/scanner control and the local web/desktop surface stay on the same Windows machine.
- Natural-language planning uses the installed Antigravity CLI by default. Do not add a hidden AI API-key dependency.
- Antigravity may propose structured print intent only; deterministic PrintAI code owns validation, geometry, preview, policy and spooler execution.

## Definition of done

A milestone is done only when acceptance criteria are met, tests are green where practical, progress docs are current, and the next concrete task is recorded.
