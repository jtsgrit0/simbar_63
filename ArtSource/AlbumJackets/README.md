# Authentic album jackets

These are publisher/catalog jackets for the five supplied MP3s. No artwork was generated, redrawn, recolored or synthesized.

| Release / artist | Supplied tracks | Primary release page | Original jacket image |
| --- | --- | --- | --- |
| Electric Shocker / 프리윌(freewill) | Electric Shocker; Bedroom Manifesto; Electric Shocker - Korean Version | [Bugs release](https://music.bugs.co.kr/album/20850049) | [1200×1200 JPEG](https://image.bugsm.co.kr/album/images/1200/208500/20850049.jpg) |
| Hey / Julywantsmore | Hey_spotdown.mp3 (embedded title: Hey) | [Bugs release](https://music.bugs.co.kr/album/20845764) | [1200×1200 JPEG](https://image.bugsm.co.kr/album/images/1200/208457/20845764.jpg) |
| No Matter How / Julywantsmore | No Matter How | [Bugs release](https://music.bugs.co.kr/album/20817216) | [1200×1200 JPEG](https://image.bugsm.co.kr/album/images/1200/208172/20817216.jpg) |

The [Apple Bedroom Manifesto track](https://music.apple.com/us/album/bedroom-manifesto/6827503438?i=6827503695&uo=4) verifies the English title corresponding to Bugs’ 방구석 선언문 on the same Electric Shocker release. Apple artist/release/track IDs and durations independently match the supplied files; all duration differences are below 0.064 seconds, consistent with MP3 frame padding. This avoids confusing the Korean freewill with unrelated artists using that name.

`originals/` retains the downloaded publisher JPEG streams unchanged. `embedded/` retains the three unique 300×300 front-cover APIC images extracted from the supplied MP3s. The publisher and embedded artwork were visually compared and show the same designs; their resolution and JPEG encoding differ. See the [comparison sheet](../../Research/AlbumJackets/source-comparison.png) and [complete track provenance](../../Research/AlbumJackets/track-jackets.json) for source URLs, MP3 tags, dimensions and SHA-256 values. All three freewill MP3s contain the same embedded front-cover hash.

Reproduce the runtime assets from the retained originals:

```powershell
uv run --with pillow==13.0.0 python Tools/prepare_album_covers.py
```

The script verifies original hashes, uniformly resizes each full image within 224×224 using LANCZOS, and saves 24-bit RGB PNGs. There is no cropping, palette quantization, repainting or synthesized detail. The three runtime PNGs total **145,154 bytes**; indices 0/3/4 share the same freewill image. Outputs are `Tools/WebGLTemplate/FastGame/art/album-covers/*.png` and `art/album-covers.json`; packing details and hashes are in [runtime-packing.json](../../Research/AlbumJackets/runtime-packing.json).

Integration contract: the manifest and images are optional local assets loaded after audio interaction, never boot preloads. The source URLs above are provenance references; runtime rendering uses the retained local PNGs.
