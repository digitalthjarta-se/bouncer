# Design

## Direction

Bouncer's development-facing web surfaces use the language of a dispatch receipt: compact,
ruled, chronological, and explicit about what arrived. The interface should feel like an
operational artifact rather than a generic dashboard.

## Color

- **Paper:** `#fffefa` for the working surface.
- **Desk:** `#ebe9e2` for the surrounding ground.
- **Carbon:** `#181713` for primary ink and hard rules.
- **Secondary ink:** `#57544c` to `#625f57`.
- **Utility amber:** `#d47b00` for live state and interaction, with `#f3b65d` for labels.
- **Error:** `#8f1d16`.

Color is restrained: amber identifies active or operational state, never decoration.

## Typography

Use Avenir Next with Avenir and Segoe UI fallbacks for interface copy, and the platform
monospace stack for machine values such as status codes and endpoints. Headings are dense and
tightly tracked; body copy remains comfortably readable.

## Composition

Prefer one strong working surface over grids of summary cards. Separate batches with hard
rules, keep metadata attached to its batch, and present addresses in a real table. On narrow
screens, preserve the table's relationships with horizontal scrolling instead of collapsing
rows into unrelated labels.

## Interaction

Controls are square, high-contrast, and named for their action. Focus states use a visible
amber outline. New receipts reveal with a short top-to-bottom clipping motion, disabled when
the user requests reduced motion.
