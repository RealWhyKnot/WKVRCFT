# Wiki source

The `.md` files in this folder are the **source of truth** for the GitHub Wiki at <https://github.com/RealWhyKnot/WKVRCFT/wiki>. They are mirrored to the wiki repo by [`.github/workflows/wiki-sync.yml`](../.github/workflows/wiki-sync.yml) on every push to `main` that touches `wiki/**`.

## Editing rules

- Always edit here, never on the GitHub web UI for the wiki. Web edits will be **overwritten** by the next sync.
- Page filename = page name. Spaces become hyphens (`Build-From-Source.md` ⇒ "Build From Source" page).
- Wiki-style links work: `[[Page Name]]` and `[[Page Name|alt text]]`.
- This `README.md` is **excluded** from the sync — GitHub Wiki uses `Home.md` as the landing page.

## Pages

- [Home](Home.md) — landing
- [Module Authors](Module-Authors.md) — write a v2 module
- [Build From Source](Build-From-Source.md)
- [Architecture](Architecture.md)
- [Registry](Registry.md)
