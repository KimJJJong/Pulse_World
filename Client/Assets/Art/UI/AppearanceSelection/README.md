# Appearance Selection UI Assets

Extracted/rebuilt from the provided reference as text-free, transparent PNG UI pieces for Unity UI.

## Files

- `ui_window_panel.png`: main parchment + wood/gold framed panel, 9-slice border `78,78,78,78`
- `ui_header_plate_blank.png`: blank teal title plate, 9-slice border `64,42,64,42`
- `ui_class_card_blank.png`: default class card with empty portrait area, 9-slice border `32,32,32,58`
- `ui_class_card_selected_blank.png`: selected class card with cyan glow, 9-slice border `40,40,40,66`
- `ui_primary_button_blank.png`: blank teal/gold primary button, 9-slice border `58,38,58,38`
- `ui_primary_button_pressed_blank.png`: highlighted/pressed primary button, 9-slice border `58,38,58,38`
- `ui_status_badge_green_blank.png`: ownership/status badge without text
- `ui_status_badge_blue_blank.png`: equipped/status badge without text
- `ui_lock_badge_blank.png`: lock badge with icon only
- `ui_close_button.png`: close button icon
- `ui_corner_cap_gold.png`: reusable decorative corner cap
- `ui_info_box_blank.png`: blank bottom description box
- `ui_appearance_selection_preview.png`: contact sheet preview only

## Unity Use

All PNGs are imported as `Sprite (2D and UI)` with alpha transparency and their `spriteBorder` values already set in the `.meta` files. For scalable UI, set the Image component `Type` to `Sliced` for the panel, cards, buttons, badges, and info box.

Keep all actual Korean UI labels in TextMeshPro objects layered above these sprites.
