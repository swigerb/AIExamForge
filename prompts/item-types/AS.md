Item Type: Active Screen (AS)

Purpose:
Assess ability to correctly configure a complex dialog or UI.

Rules:
- Use a screenshot with interactive (“active”) areas.
- Minimum of 4 active areas.
- Multiple layers are allowed.
- Drop-down controls should be used where appropriate.
- Each correct interaction earns one point (polytomous scoring).

Output Requirements:
- Identify active regions and required actions.
- Return JSON that conforms to the AS schema.

AS content schema (this is the shape of the `content` object for itemTypeCode `AS`):

- `screenshotAltText` (string, min length 10) REQUIRED
- `activeAreas` (array, minItems 4) REQUIRED
	- Each item is an object with REQUIRED fields:
		- `id` (string)
		- `label` (string)
		- `actionType` (string enum): `click` | `select` | `toggle` | `dragdrop`
		- `expectedValue` (string | boolean | number | null)

Guidance:
- Always include `screenshotAltText` at the root of `content` (do not nest under another `content` property).
- `activeAreas` must be an array of OBJECTS (not strings).
- Use `expectedValue: null` for simple `click` interactions.
- Use `actionType: select` with `expectedValue` as the exact option text/value.
- Use `actionType: toggle` with `expectedValue` as a boolean.
- Use `actionType: dragdrop` with `expectedValue` as the target/dropzone label.

Example `content` (AS):

```json
{
	"screenshotAltText": "Screenshot of an Azure portal blade used to configure a resource.",
	"activeAreas": [
		{ "id": "area-1", "label": "Region dropdown", "actionType": "select", "expectedValue": "East US" },
		{ "id": "area-2", "label": "Enable diagnostics toggle", "actionType": "toggle", "expectedValue": true },
		{ "id": "area-3", "label": "SKU dropdown", "actionType": "select", "expectedValue": "Standard" },
		{ "id": "area-4", "label": "Create button", "actionType": "click", "expectedValue": null }
	]
}
```
