# Parametric Fasteners

Parametric Fasteners is a Rhino 8 for Windows plug-in for editable screws, nuts, heat-set inserts, clearance holes, counterbores, engagement holes, and non-destructive Boolean export workflows.

Current version: `0.40.4`

## 0.40.4 main-panel health badge removal

- The main panel no longer persistently displays background maintenance or action-required badges.
- Background health scanning remains available through Navigator, Maintenance Center, and Assembly Inspector.
- Explicit update, export, and inspection failures still show actionable errors and retain safety validation.

## 0.40.3 main-panel summary cleanup

- Removed the duplicated parameter text to the left of the favorite button.
- The template tool row is now fixed as favorite, template menu, flexible space, and Quick Editor.
- Favorite state, template loading, and Quick Editor behavior remain unchanged.

## 0.40.2 responsive Quick Editor toggle

- The template toolbar switches between one and two rows using the actual available panel width.
- The **Quick Editor** toggle is measured in the active language and always remains fully visible.
- Long template names use only the remaining width, with a single-line ellipsis and full tooltip.
- Chinese, English, narrow panels, and high-DPI layouts remain free of horizontal scrolling.

## 0.40.1 health-check noise reduction

- Fresh placement and updates no longer report false rebuild requirements from intermediate transactions or regenerated Breps.
- Background health checks are structural and lightweight; geometry and Boolean validation remain explicit inspection operations.
- A compact maintenance/action-required badge replaces intrusive background warnings.
- Preview and Boolean configuration switches are no longer counted as component failures.

## Language

Open **More → Settings → Language** and choose:

- **Auto** — Simplified Chinese when Rhino uses Simplified Chinese; English for other Rhino languages.
- **简体中文** — always use Simplified Chinese.
- **English** — always use English.

Open plug-in panels and dialogs update immediately without discarding input. A command already in progress keeps the language it started with; the next command uses the new language. The Rhino sidebar caption updates after Rhino restarts.

Changing language does not rename public commands, component IDs, JSON fields, metadata keys, or the stable layers, groups, objects, and materials stored in 3DM files.

## Main workflow

1. Open the panel with `ParametricFasteners`.
2. Choose a fastener type and size, then configure assembly and hole parameters.
3. Run smart placement and click the first host surface.
4. Select one or more control points and choose **Apply Update** to apply the current panel template.
5. Use **Insert into Rhino**, STEP, STL, or the Output Center to generate non-destructive deliverables.

The principal screw assembly modes are **Thread Engagement**, **Engagement Only**, and **Nut Fastened**. The plug-in also supports standard and nylon-insert hex nuts, heat-set inserts, batch placement, assembly inspection, relinking, statistics, custom definitions, and atomic multi-format output.

## Installation

Use the packaged Yak or the offline installer. Do not install the `.rhp` directly from Downloads, a temporary directory, or removable storage.

See [English installation instructions](docs/INSTALL.en.md) and [English user workflow](docs/USER_FLOWS.en.md).

## Compatibility

- Rhino 8 for Windows
- Minimum service release represented by Yak distribution tag `rh8_18-win`
- Plug-in version `0.40.4`
- Component schema v21
- Template schema v5
- Template library schema v2

## Release notes

- [0.40.4 main-panel health badge removal](docs/RELEASE_0.40.4.md)
- [0.40.3 main-panel summary cleanup](docs/RELEASE_0.40.3.md)
- [0.40.2 responsive Quick Editor toggle](docs/RELEASE_0.40.2.md)
- [0.40.1 quiet health checks](docs/RELEASE_0.40.1.md)
- [0.40.0 bilingual interface](docs/RELEASE_0.40.0.md)
- [0.39.0 interface consolidation](docs/RELEASE_0.39.0.md)
