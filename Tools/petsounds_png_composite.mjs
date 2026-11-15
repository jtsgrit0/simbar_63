// Lossless technical PNG assembly. New textures are supplied by ImageGen/SVG;
// untouched pixels retain the original file's raw RGBA, including transparency.
import fs from 'node:fs';
import crypto from 'node:crypto';
import zlib from 'node:zlib';
import assert from 'node:assert/strict';

const args=Object.fromEntries(process.argv.slice(2).reduce((pairs,value,index,array)=>index%2?pairs:[...pairs,[value,array[index+1]]],[]));
for(const name of ['--original','--before','--after','--config','--output','--report'])assert(args[name],`Missing ${name}`);
const signature=Buffer.from([137,80,78,71,13,10,26,10]);
const paeth=(a,b,c)=>{const p=a+b-c,aa=Math.abs(p-a),bb=Math.abs(p-b),cc=Math.abs(p-c);return aa<=bb&&aa<=cc?a:bb<=cc?b:c;};
function decode(file){
  const bytes=fs.readFileSync(file);assert(bytes.subarray(0,8).equals(signature),'Not a PNG');
  let header;const blocks=[],metadata=[];
  for(let offset=8;offset<bytes.length;){
    const size=bytes.readUInt32BE(offset),type=bytes.toString('ascii',offset+4,offset+8),data=bytes.subarray(offset+8,offset+8+size);
    if(type==='IHDR')header=data;
    if(type==='IDAT')blocks.push(data);
    if(['sRGB','gAMA','cHRM','iCCP','pHYs'].includes(type))metadata.push({type,data});
    offset+=size+12;
  }
  assert(header);const width=header.readUInt32BE(0),height=header.readUInt32BE(4),depth=header[8],type=header[9];
  assert.equal(depth,8);assert([2,6].includes(type));assert.equal(header[10],0);assert.equal(header[11],0);assert.equal(header[12],0);
  const channels=type===6?4:3,stride=width*channels,filtered=zlib.inflateSync(Buffer.concat(blocks));
  assert.equal(filtered.length,(stride+1)*height);
  const native=Buffer.alloc(stride*height);let offset=0;
  for(let y=0;y<height;y++){
    const filter=filtered[offset++];assert(filter<=4);
    for(let x=0;x<stride;x++){
      const p=y*stride+x,left=x>=channels?native[p-channels]:0,up=y?native[p-stride]:0,corner=y&&x>=channels?native[p-stride-channels]:0;
      const predictor=filter===1?left:filter===2?up:filter===3?Math.floor((left+up)/2):filter===4?paeth(left,up,corner):0;
      native[p]=(filtered[offset++]+predictor)&255;
    }
  }
  const rgba=Buffer.alloc(width*height*4);
  for(let p=0;p<width*height;p++){native.copy(rgba,p*4,p*channels,p*channels+channels);if(channels===3)rgba[p*4+3]=255;}
  return{width,height,rgba,metadata,bytes};
}
const crcTable=Uint32Array.from({length:256},(_,index)=>{let value=index;for(let bit=0;bit<8;bit++)value=value&1?0xedb88320^(value>>>1):value>>>1;return value>>>0;});
function chunk(type,data){
  const name=Buffer.from(type),payload=Buffer.concat([name,data]),result=Buffer.alloc(data.length+12);let crc=0xffffffff;
  for(const value of payload)crc=crcTable[(crc^value)&255]^(crc>>>8);
  result.writeUInt32BE(data.length,0);payload.copy(result,4);result.writeUInt32BE((crc^0xffffffff)>>>0,result.length-4);return result;
}
function encode(image){
  const header=Buffer.alloc(13);header.writeUInt32BE(image.width,0);header.writeUInt32BE(image.height,4);header[8]=8;header[9]=6;
  const stride=image.width*4,raw=Buffer.alloc((stride+1)*image.height);
  for(let y=0;y<image.height;y++)image.rgba.copy(raw,y*(stride+1)+1,y*stride,(y+1)*stride);
  return Buffer.concat([signature,chunk('IHDR',header),...image.metadata.map(value=>chunk(value.type,value.data)),chunk('IDAT',zlib.deflateSync(raw,{level:9})),chunk('IEND',Buffer.alloc(0))]);
}
function inside(x,y,points){
  let result=false;
  for(let i=0,j=points.length-1;i<points.length;j=i++){
    const [xi,yi]=points[i],[xj,yj]=points[j];
    if((yi>y)!==(yj>y)&&x<(xj-xi)*(y-yi)/(yj-yi)+xi)result=!result;
  }
  return result;
}
function near(x,y,points){
  if(inside(x,y,points))return true;
  for(let i=0;i<points.length;i++){
    const a=points[i],b=points[(i+1)%points.length],dx=b[0]-a[0],dy=b[1]-a[1],length=dx*dx+dy*dy;
    const t=length?Math.max(0,Math.min(1,((x-a[0])*dx+(y-a[1])*dy)/length)):0;
    if((x-a[0]-t*dx)**2+(y-a[1]-t*dy)**2<=.8**2)return true;
  }
  return false;
}
const original=decode(args['--original']),before=decode(args['--before']),after=decode(args['--after']);
for(const image of [before,after])assert(image.width===original.width&&image.height===original.height,'Image dimensions differ');
const config=JSON.parse(fs.readFileSync(args['--config'],'utf8')),output=Buffer.from(original.rgba);
const patches=config.floorPatches??(config.image?[config]:[]);let selected=0,unchanged=0,deniedCanvasDifferences=0;
for(let p=0;p<original.width*original.height;p++){
  const offset=p*4;let different=false;
  for(let c=0;c<4;c++)if(before.rgba[offset+c]!==after.rgba[offset+c])different=true;
  if(!different){unchanged++;continue;}
  const x=p%original.width+.5,y=Math.floor(p/original.width)+.5;
  const floorAllowed=patches.some(patch=>(patch.polygons??[]).some(polygon=>near(x,y,polygon))&&!(patch.preservePolygons??[]).some(polygon=>inside(x,y,polygon)));
  const spriteAllowed=(config.sprites??[]).some(sprite=>{const [sx,sy,w,h]=sprite.rect;return x>=sx&&x<sx+w&&y>=sy&&y<sy+h;});
  if(floorAllowed||spriteAllowed){after.rgba.copy(output,offset,offset,offset+4);selected++;}
  else deniedCanvasDifferences++;
}
const bytes=encode({...original,rgba:output});fs.writeFileSync(args['--output'],bytes);
const roundtrip=decode(args['--output']);assert(roundtrip.rgba.equals(output),'PNG raw RGBA roundtrip failed');
const report={width:original.width,height:original.height,selectedGeneratedPixels:selected,unchangedCanvasPixels:unchanged,deniedCanvasDifferences,
  untouchedRgbaPreserved:true,pngRoundtripExact:true,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),bytes:bytes.length,node:process.version};
fs.writeFileSync(args['--report'],JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
