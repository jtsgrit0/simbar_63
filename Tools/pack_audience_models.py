from pathlib import Path
from base64 import b64decode
from playwright.sync_api import sync_playwright
import json, sys
root=Path(__file__).resolve().parent
if len(sys.argv)!=6:
    raise SystemExit('usage: pack_audience_models.py SHEET1 SHEET2 SHEET3 SHEET4 OUTPUT_DIRECTORY')
with sync_playwright() as p:
    browser=p.chromium.launch(channel='chrome')
    page=browser.new_page()
    page.add_script_tag(content=(root/'pack_audience_models.mjs').read_text().replace('export function','function'))
    names=list(map(Path,sys.argv[1:5]))
    from base64 import b64encode
    image_data=['data:image/png;base64,'+b64encode(path.read_bytes()).decode() for path in names]
    result=page.evaluate("""async sources=>{
      const sheets=await Promise.all(sources.map(async src=>{
        const im=new Image();im.src=src;await im.decode();
        const c=document.createElement('canvas');c.width=im.width;c.height=im.height;
        const x=c.getContext('2d');x.drawImage(im,0,0);
        return {width:c.width,height:c.height,data:x.getImageData(0,0,c.width,c.height).data};
      }));
      const {atlas,rigs,width,height}=packAudience(sheets);
      const c=document.createElement('canvas');c.width=width;c.height=height;
      c.getContext('2d').putImageData(new ImageData(atlas,width,height),0,0);
      return {png:c.toDataURL().split(',')[1],rigs};
    }""",image_data)
    dest=Path(sys.argv[5]);dest.mkdir(parents=True,exist_ok=True)
    (dest/'patrons.png').write_bytes(b64decode(result['png']))
    (dest/'patrons.json').write_text(json.dumps(result['rigs'],separators=(',',':'))+'\n',encoding='ascii')
    print('packed',len(result['rigs']),(dest/'patrons.png').stat().st_size)
