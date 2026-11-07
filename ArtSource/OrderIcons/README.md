# Customer order icons

`MenuIcons.source.png` is the unchanged transparent 3×2 source produced by the
built-in ImageGen tool. Its exact prompt is in `MenuIcons.prompt.md`.

`Tools/prepare_order_icons.py` splits the six equal source cells, ignores alpha
below 8 in its working copy, crops transparent margins, and uses nearest-neighbor
sampling to center each icon within a 32×32 runtime cell. It performs no redraw or
recolor. `packing.json` records bounds and SHA-256 hashes. Run:

```powershell
uv run --with pillow python Tools/prepare_order_icons.py
```

The single runtime atlas is `Tools/WebGLTemplate/FastGame/art/order-icons.png`,
192×32 pixels. Columns are Faust, AMF, pepperoni pizza, chili cheese fries, chili
cheese chips, and fried chicken. All six are live menu items. Fried chicken is
item 5 (keyboard shortcut 6), costs ₩18,000, and uses food plating rather than
drink pouring. The original five menu IDs and prices are unchanged.

The renderer reads actual pending orders, shows an icon only while that customer
has an unserved order, and follows the customer's current position. Balloon
cards dodge close neighbours while their pointers remain aimed at the correct
customer. Serving, expiration, and closing remove the icon without a cached
presentation timer.
