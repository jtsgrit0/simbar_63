# Owner street backdrops

The built-in ImageGen tool produced two separate original nighttime pixel-art
illustrations. They adapt observed street and entrance features to a game
backdrop; they are not surveyed replicas or embedded map screenshots.

| Owner | Active source | Observed architecture |
| --- | --- | --- |
| Jessy / Freebird | `FreebirdStreet.source.png` | Gray stone and glass building, timber entrance framing, black/white basement sign, descending B1 stairs, concert posters and a narrow Hongdae lane |
| Edison / FF | `FFStreet.source.png` | Burgundy canopy, gold slanted FF mark and lanterns, red brick upper floors, white balcony bands and recessed entrance |
| Jaiden / Pet Sounds | Existing `seoul.png` | Retained Seoul nighttime background |

## Location references

Both Google Maps and Naver Map were opened and visually inspected. Naver's
street views were labeled **August 2026 (latest) 3D**. Screens and full research
notes stay in ignored `Temp/OwnerBackdrops-20261111/references`.

- Freebird: [Naver place](https://map.naver.com/p/entry/place/2045364198?placePath=%2Fhome),
  [Google place](https://www.google.com/maps/search/?api=1&query=Freebird+Reboot&query_place_id=ChIJ1QjlAgCZfDURiElUIZ_wBNg),
  [2026 organizer's address confirmation](https://event-us.kr/nomercycompany/event/123635),
  [entrance photographs](https://www.k-dj.jp/ko/clubs/club-freebird-reboot-seoul/).
  The current venue is **19-22 Wausan-ro17-gil, B1**, distinct from the similarly
  named Google result at number 15.
- FF: [Naver place](https://map.naver.com/p/entry/place/20917624?placePath=%2Fhome),
  [Google address search](https://www.google.com/maps/search/?api=1&query=Club+FF+12+Wausan-ro+17-gil+Seoul),
  **12 Wausan-ro17-gil**. The current street view was used for the final facade;
  older orange-cladding/giant-guitar photographs describe a historical exterior.

The active and superseded draft prompts are recorded in `prompts.json`.
`Tools/prepare_owner_backdrops.py` uses nearest-neighbor cover sizing and
256-color encoding to create `art/seoul-freebird.png` and `art/seoul-ff.png`
at 640×360. The owner-venue preparation pipeline calls this helper. Native
boot and direct game startup load both assets; lobby and world rendering select
the same owner, and the cached street is refreshed on owner or size changes.
These backgrounds ship in the native website and its Electron Windows player.
