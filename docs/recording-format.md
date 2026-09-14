# Macro file format

Macro files are UTF-8 JSON documents with a `.macro.json` suffix. The format is versioned so future releases can reject or migrate files explicitly.

## Version 1 shape

```json
{
  "format": "mouse-keyboard-macro",
  "version": 1,
  "name": "Example macro",
  "created_at": "2026-01-01T00:00:00Z",
  "screen": {
    "width": 1920,
    "height": 1080,
    "dpi_scale": 1.0
  },
  "actions": [
    {
      "type": "mouse_move",
      "after_ms": 0,
      "x": 420,
      "y": 260
    },
    {
      "type": "mouse_button",
      "after_ms": 85,
      "x": 420,
      "y": 260,
      "button": "left",
      "state": "down"
    },
    {
      "type": "mouse_button",
      "after_ms": 42,
      "x": 420,
      "y": 260,
      "button": "left",
      "state": "up"
    },
    {
      "type": "keyboard",
      "after_ms": 80,
      "key": "A",
      "virtual_key": 65,
      "state": "down",
      "extended": false
    },
    {
      "type": "keyboard",
      "after_ms": 40,
      "key": "A",
      "virtual_key": 65,
      "state": "up",
      "extended": false
    }
  ]
}
```

## Rules

- `format` is always `mouse-keyboard-macro`.
- `version` must be the supported positive version, currently `1`.
- `name` is user-visible text and is never used as a filesystem path.
- `created_at` is an ISO 8601 timestamp.
- `screen` describes the virtual desktop at record time. Its dimensions and DPI scale must be positive.
- `after_ms` is the non-negative delay since the previous action.
- Action types are explicit and finite. Unknown action types are invalid.
- Mouse button actions include `x`, `y`, `button`, and `state`.
- Mouse wheel actions include `x`, `y`, and a non-zero `delta`.
- Keyboard actions include `key`, `virtual_key`, `state`, and `extended`.
- Key names and mouse buttons use canonical names produced by the Windows adapter.
- A macro must not end while a key or mouse button is held. The recorder ignores auto-repeat down events and unmatched release events so normal typing produces a balanced document.
- Timestamps and screen metadata are descriptive; they must not be trusted as permission or security data.

The version 1 schema is formalized in `MacroJsonSerializer`. Changes that alter the file contract require a version increment and a migration or a clear incompatibility error.
