"""Pack the generated guest greetings; retain illustration, aspect and alpha."""
from pathlib import Path
import json
from PIL import Image
from optimize_runtime_atlases import save_runtime_png

ROOT=Path(__file__).resolve().parent.parent
SOURCE=ROOT/'ArtSource/PixelArt/V4'
OUTPUT=ROOT/'Tools/WebGLTemplate/FastGame/art/v4-guest-social.png'

def cells(path):
    source=Image.open(path).convert('RGBA')
    result=[]
    for row in range(8):
        line=[]
        for col in range(8):
            cell=source.crop((round(col*source.width/8),round(row*source.height/8),round((col+1)*source.width/8),round((row+1)*source.height/8)))
            strong=cell.getchannel('A').point(lambda a:255 if a>128 else 0)
            box=strong.getbbox()
            if not box:raise ValueError(f'Missing {path.name} cell {row},{col}')
            foot=strong.crop((0,max(box[1],box[3]-10),cell.width,box[3])).getbbox()
            anchor=(foot[0]+foot[2])/2
            line.append((cell,box,anchor))
        result.append(line)
    return result

def place(target,cell,box,anchor,x,y,scale):
    # Cell geometry is normalized without stretching or changing the drawing.
    crop=cell.crop(box)
    size=(max(1,round(crop.width*scale)),max(1,round(crop.height*scale)))
    sprite=crop.resize(size,Image.Resampling.NEAREST)
    target.paste(sprite,(round(x-(anchor-box[0])*scale),y-size[1]))

def main():
    sheets=[]
    for name in ['GuestGreetingsA','GuestGreetingsB']:
        path=SOURCE/(name+'.png');raw=SOURCE/(name+'-original.png')
        if not raw.exists():raw.write_bytes(path.read_bytes())
        sheets.append(cells(raw))
    height=max(box[3]-box[1] for sheet in sheets for row in sheet for _,box,_ in row)
    runtime=Image.new('RGBA',(512,2048))
    for sheet_index,sheet in enumerate(sheets):
        normalized=Image.new('RGBA',(2048,2048))
        for row in range(8):
            for col in range(8):
                cell,box,anchor=sheet[row][col]
                place(normalized,cell,box,anchor,col*256+128,row*256+240,208/height)
        normalized.save(SOURCE/('GuestGreetings'+('A' if sheet_index==0 else 'B')+'.png'),optimize=True)
        for style in range(4):
            for direction in range(4):
                source_row=style*2+direction//2
                source_base=(direction%2)*4
                output_row=(sheet_index*4+style)*4+direction
                for frame,pose in enumerate([0,1,1,0,2,3,3,2]):
                    cell,box,anchor=sheet[source_row][source_base+pose]
                    place(runtime,cell,box,anchor,frame*64+32,output_row*64+60,52/height)
    # Quantization compresses only the retained generated pixels, including alpha.
    save_runtime_png(runtime.quantize(colors=256,method=Image.Quantize.FASTOCTREE,dither=Image.Dither.NONE),OUTPUT)
    metadata={'sourceGrid':[8,8],'sourceCell':256,'runtimeSize':list(runtime.size),'cell':64,
        'rows':'styleIndex*4+directionIndex','directions':['FR','BR','BL','FL'],
        'styles':['Casual','Suit','Beach','Worker','Farmer','Adventurer','Casual2','Punk'],
        'highfiveColumns':[0,1,2,3],'hugColumns':[4,5,6,7],
        'sourcePoseSequence':[0,1,1,0,2,3,3,2],'pivot':[32,60],'visibleHeight':52,
        'sourceMaxHeight':height,'bytes':OUTPUT.stat().st_size}
    (SOURCE/'GuestGreetings.packing.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf8')
    print(json.dumps(metadata))

if __name__=='__main__':main()
