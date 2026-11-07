// Optional researched jackets are requested only after a music interaction.
export class AlbumArtLoader{
  constructor({baseURL,covers={},fetcher=(...args)=>globalThis.fetch(...args),ImageClass=globalThis.Image,onerror=()=>{}}){
    this.baseURL=baseURL;this.covers=covers;this.fetcher=fetcher;this.ImageClass=ImageClass;this.onerror=onerror;
    this.manifest=null;this.requests=new Map();this.images=new Map();this.destroyed=false;
  }
  ensure(index){
    if(this.destroyed||!Number.isInteger(index)||index<0)return Promise.resolve(null);
    if(this.covers[index])return Promise.resolve(this.covers[index]);
    if(this.requests.has(index))return this.requests.get(index);
    const pending=this.load(index).catch(error=>{this.requests.delete(index);if(!this.destroyed)this.onerror(error);return null;});
    this.requests.set(index,pending);return pending;
  }
  async descriptor(){
    if(!this.manifest)this.manifest=(async()=>{
      const response=await this.fetcher(new URL('album-covers.json',this.baseURL));
      if(!response.ok)throw new Error('Album jacket manifest unavailable');
      const data=await response.json();
      if(data.version!==1||!Array.isArray(data.tracks))throw new Error('Invalid album jacket manifest');
      return data.tracks.filter(track=>Number.isInteger(track.index)&&track.index>=0&&typeof track.cover==='string'&&/^album-covers\/[a-z0-9-]+\.png$/.test(track.cover));
    })().catch(error=>{this.manifest=null;throw error;});
    return this.manifest;
  }
  async load(index){
    const tracks=await this.descriptor();if(this.destroyed)return null;
    const track=tracks.find(track=>track.index===index);if(!track)return null;
    if(!this.images.has(track.cover))this.images.set(track.cover,(async()=>{
      const image=new this.ImageClass();image.src=new URL(track.cover,this.baseURL).href;await image.decode();return image;
    })().catch(error=>{this.images.delete(track.cover);throw error;}));
    const image=await this.images.get(track.cover);if(this.destroyed)return null;
    for(const entry of tracks)if(entry.cover===track.cover)this.covers[entry.index]=image;
    return image;
  }
  destroy(){this.destroyed=true;this.requests.clear();this.images.clear();}
}
