# Parametric Fasteners — User Workflow

## Choose the interface language

Open **More → Settings → Language**, then choose **Auto**, **Simplified Chinese**, or **English**. Plug-in-owned windows refresh immediately and preserve their current values. Restart Rhino only when you want the sidebar caption to change.

## Place a screw

1. Configure the creation template in the main panel.
2. Choose **Place / Bind**.
3. Move the cursor over the first closed host and click.
4. Confirm the engagement host when automatic host recognition cannot determine a unique result.

The moving preview stays lightweight; exact hosts, cutters, and geometry are validated after the click. A failed validation writes nothing and placement remains active.

## Edit components

- Select one control point to use the compact editor.
- Select multiple control points and choose **Apply Update** to apply the main-panel template.
- Scene selection identifies targets only. It does not overwrite the creation template.
- Use **Read** only when you intentionally want the selected component parameters loaded into the panel.

## Inspect and repair

Use **Assembly Inspector** to locate, select, adjust, or delete problem components. Use **Maintenance** for deterministic rebuild, explicit relinking, and orphan cleanup. Document opening performs a read-only health scan and never silently changes the model.

Background health checks are structural and non-intrusive. A compact **Maintenance required** badge indicates deterministic repair work; **Action required** indicates damaged or unbound components. Preview/Boolean switches and presentation differences remain configuration information rather than component failures. Click the badge to open Maintenance Center. Full Boolean and assembly checks remain explicit operations.

## Export

Quick exports and the Output Center generate Boolean host copies from current component data. Export validation is isolated to the selected hosts and their direct bindings; unrelated damaged components do not block a valid selected-host export.

English mode uses `Delivery`, `Assembly`, `Model`, and `Print` in generated delivery paths. Internal 3DM layer, group, object, and material names remain stable for cross-language consistency.

## Quick Editor toggle

The **Quick Editor** toggle always receives display priority in the main-panel template toolbar. Narrow panels, long template names, and English text automatically switch the toolbar to two rows. The template name is ellipsized with its full value available in the tooltip. Turning the toggle off immediately hides the compact editor; turning it back on restores automatic display for a valid single selection.

## Template tool row

The first line is now the only current-template parameter summary. The tool row no longer repeats the fastener type or size; it keeps favorite and template controls on the left and the Quick Editor toggle on the right.

## Background health status

The main panel no longer persistently displays background health badges. Use Fastener Navigator, Refresh / Maintenance, Assembly Inspector, or the More menu to review document health. Explicit update, export, and inspection failures continue to show their actionable reason in the main panel.
