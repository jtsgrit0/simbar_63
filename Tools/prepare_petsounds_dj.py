"""Build the approved Petsounds DJ room from preserved ImageGen source assets.

Only SVG clipping, source cropping, technical color gains and compositing are
used. ChromeCanvas renders the reviewed composite. The dependency-free Node
assembler retains the original raw RGBA for every untouched pixel, avoiding
extra premultiplied-alpha rounding on the sign, stage, bar and rear sofas.
"""
from pathlib import Path
import argparse,base64,copy,hashlib,html,json,shutil,struct,subprocess,tempfile
from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable

ROOT=Path(__file__).resolve().parent.parent
SOURCE=ROOT/'ArtSource/PixelArt/OwnerVenues/PetSounds/DJFlightCase-v1'
OUTPUT=ROOT/'Tools/WebGLTemplate/FastGame/art'

def composite_svg(variant, original):
 image=lambda raw: 'data:image/png;base64,'+base64.b64encode(raw).decode('ascii')
 parts=['<svg xmlns="http://www.w3.org/2000/svg" width="960" height="558" viewBox="0 0 960 558" style="image-rendering:pixelated">',
  '<image href="'+image(original)+'" width="960" height="558"/>']
 patches=variant.get('floorPatches')
 if patches is None:patches=[variant] if variant.get('image') else []
 for patch_index,patch in enumerate(patches):
  floor_parts=[]
  preservation=''
  if patch.get('preservePolygons'):
   # The original PNG is slightly translucent (alpha 253). Excluding protected
   # regions from the patch preserves its exact original color/alpha; painting
   # that PNG a second time would incorrectly make protected pixels more opaque.
   shapes=''.join('<polygon points="'+html.escape(' '.join(f'{x},{y}' for x,y in polygon),quote=True)+'" fill="black"/>' for polygon in patch['preservePolygons'])
   floor_parts.append('<defs><mask id="keepOriginal" maskUnits="userSpaceOnUse" x="0" y="0" width="960" height="558" style="mask-type:luminance"><rect width="960" height="558" fill="white"/>'+shapes+'</mask></defs>')
   preservation=' mask="url(#keepOriginal)"'
  if patch.get('image'):
   edited=Path(patch['image']).read_bytes()
   polygons=patch.get('polygons');assert polygons,'Only explicit root-supplied approved floor regions may change'
   clips=''.join('<polygon points="'+html.escape(' '.join(f'{x},{y}' for x,y in polygon),quote=True)+'"/>' for polygon in polygons)
   floor_parts.append('<defs><clipPath id="approved" clipPathUnits="userSpaceOnUse">'+clips+'</clipPath></defs>')
   feather=float(patch.get('feather',0));assert 0<=feather<=4
   mask=''
   if feather:
    # Blurring the alpha mask only softens the approved boundary. The original
    # hard clip stays in place, so the patch cannot change even one pixel beyond
    # its authorized polygon (a stricter bound than a one-pixel expansion).
    floor_parts.append(f'<defs><filter id="floorFeather" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="{feather}"/></filter>'
     '<mask id="floorMask" maskUnits="userSpaceOnUse" x="0" y="0" width="960" height="558" style="mask-type:alpha"><g fill="white" filter="url(#floorFeather)">'+clips+'</g></mask></defs>')
    mask=' mask="url(#floorMask)"'
   filters=[]
   if patch.get('floorRGBGain'):
    gains=[float(value) for value in patch['floorRGBGain']];assert len(gains)==3 and all(0<value<=4 for value in gains)
    floor_parts.append('<defs><filter id="floorRGB" color-interpolation-filters="sRGB"><feComponentTransfer>'
     +''.join(f'<feFunc{channel} type="linear" slope="{gain}" intercept="0"/>' for channel,gain in zip('RGB',gains))
     +'</feComponentTransfer></filter></defs>')
    filters.append('url(#floorRGB)')
   if patch.get('floorFilter') and patch['floorFilter']!='none':filters.append(str(patch['floorFilter']))
   floor_filter=html.escape(' '.join(filters) or 'none',quote=True)
   floor_parts.append('<g'+preservation+'><image href="'+image(edited)+'" width="960" height="558" preserveAspectRatio="none" clip-path="url(#approved)"'+mask+' style="filter:'+floor_filter+'"/></g>')
  floor_svg=''.join(floor_parts)
  for identifier in ['keepOriginal','approved','floorFeather','floorMask','floorRGB']:
   floor_svg=floor_svg.replace('id="'+identifier+'"','id="'+identifier+'-'+str(patch_index)+'"').replace('#'+identifier+')','#'+identifier+'-'+str(patch_index)+')')
  parts.append(floor_svg)
 for sprite in variant.get('sprites',[]):
  raw=Path(sprite['image']).read_bytes()
  assert raw[:8]==b'\x89PNG\r\n\x1a\n','Sprite input must be the root-provided PNG'
  image_width,image_height=struct.unpack('>II',raw[16:24])
  sx,sy,sw,sh=sprite.get('sourceRect',[0,0,image_width,image_height]);x,y,w,h=sprite['rect']
  assert sw>0 and sh>0 and w>0 and h>0 and sx>=0 and sy>=0 and sx+sw<=image_width and sy+sh<=image_height
  # The nested SVG viewBox crops the source; no raster pixel editing occurs.
  parts.append(f'<svg x="{x}" y="{y}" width="{w}" height="{h}" viewBox="{sx} {sy} {sw} {sh}" preserveAspectRatio="none" overflow="hidden" style="image-rendering:pixelated">'
   f'<image href="{image(raw)}" width="{image_width}" height="{image_height}"/></svg>')
 parts.append('</svg>')
 return ''.join(parts)

def prepare_petsounds_dj(output=OUTPUT, baseline=None, node=None, chromium=None):
    layout_path=SOURCE/'layout.json'
    layout=json.loads(layout_path.read_text(encoding='utf8'))
    original_path=Path(baseline).resolve() if baseline else SOURCE/layout['baseline']
    original=original_path.read_bytes()
    assert original[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',original[16:24])==(960,558)
    assert layout['sprites'][0]['sourceRect']==[260,205,1015,662]
    assert layout['sprites'][0]['rect']==[482,228,92,60]
    resolved=copy.deepcopy(layout)
    for patch in resolved.get('floorPatches',[]):patch['image']=str((SOURCE/patch['image']).resolve())
    for sprite in resolved.get('sprites',[]):sprite['image']=str((SOURCE/sprite['image']).resolve())
    target=Path(output)
    if target.suffix.lower()!='.png':target=target/'venue.png'
    target=target.resolve();target.parent.mkdir(parents=True,exist_ok=True)
    work=Path(tempfile.mkdtemp(prefix='petsounds-dj-',dir=ROOT/'Temp')).resolve()
    assert work.parent==(ROOT/'Temp').resolve()
    (work/'layout.json').write_text(json.dumps(layout,ensure_ascii=False,indent=2),encoding='utf8')
    svgs={'before':composite_svg({},original),'after':composite_svg(resolved,original)}
    with sync_playwright() as pw:
        browser=pw.chromium.launch(executable_path=chromium or chromium_executable(None))
        browser_version=browser.version
        page=browser.new_page()
        for label,svg in svgs.items():
            (work/(label+'.svg')).write_text(svg,encoding='utf8')
            png=page.evaluate("""async source=>{const i=new Image();i.src='data:image/svg+xml;base64,'+source;await i.decode();const c=document.createElement('canvas');c.width=960;c.height=558;const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.drawImage(i,0,0,960,558);return c.toDataURL('image/png').split(',')[1];}""",base64.b64encode(svg.encode()).decode())
            (work/(label+'.png')).write_bytes(base64.b64decode(png))
        browser.close()
    executable=node or shutil.which('node')
    if not executable:
        fallback=ROOT/'Temp/RuntimeTools/node-v26.13.0-win-x64/node.exe'
        executable=str(fallback) if fallback.is_file() else None
    if not executable:raise RuntimeError('Node 20+ is required; put node on PATH or pass --node.')
    report_path=work/'composition-report.json'
    subprocess.run([str(executable),str(ROOT/'Tools/petsounds_png_composite.mjs'),'--original',str(original_path),'--before',str(work/'before.png'),'--after',str(work/'after.png'),'--config',str(work/'layout.json'),'--output',str(target),'--report',str(report_path)],check=True,capture_output=True,text=True)
    report=json.loads(report_path.read_text())
    report.update(file=str(target),sourceDirectory=str(SOURCE),workDirectory=str(work),chromium=browser_version,
        inputSha256={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [original_path,layout_path,*[SOURCE/p['image'] for p in layout['floorPatches']],*[SOURCE/p['image'] for p in layout['sprites']]]})
    report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    return report

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=OUTPUT,help='Output directory or PNG path')
    parser.add_argument('--baseline',type=Path,help='Optional newly rebuilt neon/base plate; default preserves the approved pre-DJ source')
    parser.add_argument('--node',help='Node executable')
    parser.add_argument('--chromium',help='Chromium executable')
    args=parser.parse_args()
    print(json.dumps(prepare_petsounds_dj(args.output,args.baseline,args.node,args.chromium),ensure_ascii=False,indent=2))
