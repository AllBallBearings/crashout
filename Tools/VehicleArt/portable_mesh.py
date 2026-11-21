"""CPU authoring/export/inspection backend for machines without a Blender GPU context.

The same surface definitions feed Blender or this backend. Previews are geometry
reviews with analytical studio shading, not screenshots of Unity or Blender.
"""
import numpy as np
import math, struct
from collections import defaultdict
from PIL import Image, ImageDraw

class Vector(np.ndarray):
    def __new__(cls,v): return np.asarray(v,dtype=float).view(cls)
    x=property(lambda s:s[0],lambda s,v:s.__setitem__(0,v))
    y=property(lambda s:s[1],lambda s,v:s.__setitem__(1,v))
    z=property(lambda s:s[2],lambda s,v:s.__setitem__(2,v))
    length=property(lambda s:float(np.linalg.norm(s)))
    def cross(s,v): return Vector(np.cross(s,v))
    def normalized(s): return Vector(s/max(1e-12,s.length))
    def normalize(s): s[:]=s.normalized()

objects=[]; materials={}
class Mesh:
    def __init__(self,name,vs,faces,role,part,normals=None):
        self.name=name; self.v=np.array(vs,float); self.role=role; self.part=part
        self.rotation_euler=[0.,0.,0.]
        self.f=np.array([(f[0],f[i],f[i+1]) for f in faces for i in range(1,len(f)-1)],int)
        self.n=np.zeros_like(self.v)
        if len(self.f):
            cross=np.cross(self.v[self.f[:,1]]-self.v[self.f[:,0]],self.v[self.f[:,2]]-self.v[self.f[:,0]])
            for j in range(3): np.add.at(self.n,self.f[:,j],cross)
            self.n/=np.maximum(1e-12,np.linalg.norm(self.n,axis=1))[:,None]
        if normals is not None:self.n=np.array(normals)
    def get(self,k):return self.part if k=='part' else self.role

def obj(name,verts,faces,role='paint',group='Body',thickness=0):
    m=Mesh(name,verts,faces,role,group)
    if thickness:
        count=len(m.v); front=m.v.copy(); ns=m.n.copy(); tris=m.f.copy()
        # Include the actual sheet edges; a torn panel is never a one-sided disappearing plane.
        edge=defaultdict(list)
        for f in faces:
            for a,b in zip(f,(*f[1:],f[0])):edge[tuple(sorted((a,b)))].append((a,b))
        side=[]
        for uses in edge.values():
            if len(uses)==1:
                a,b=uses[0]; side.extend([(b,a,a+count),(b,a+count,b+count)])
        m.v=np.vstack((front,front-ns*thickness)); m.n=np.vstack((ns,-ns))
        m.f=np.vstack((tris,tris[:,::-1]+count,np.array(side,int).reshape(-1,3)))
    objects.append(m); return m

def box(name,center,size,role='trim',group='Body',bevel=.015):
    h=np.array(size)*.5; b=min(bevel,min(h)*.9); inner=h-b
    vertices=[]; normals=[]; faces=[]
    # Bevelled, closed box with planar faces and smoothly rounded edges.
    steps=[-1,-.75,0,.75,1] if b else [-1,1]
    for axis in range(3):
        u=(axis+1)%3; v=(axis+2)%3
        for sign in [-1,1]:
            start=len(vertices)
            for a in steps:
                for c in steps:
                    p=np.zeros(3); p[axis]=sign*h[axis]
                    # Exact boundary/support-ring coordinates, not a rounded toy cube.
                    def coord(t,k):return math.copysign(inner[k]+b*(abs(t)-.5)*2,t) if abs(t)>.5 else t*2*inner[k]
                    p[u]=coord(a,u); p[v]=coord(c,v)
                    q=np.clip(p,-inner,inner); n=p-q; n/=max(1e-12,np.linalg.norm(n))
                    vertices.append(q+n*b+center); normals.append(n)
            n=len(steps)
            for i in range(n-1):
                for j in range(n-1):
                    a=start+i*n+j; f=(a,a+n,a+n+1,a+1)
                    if sign<0:f=f[::-1]
                    faces.append(f)
    m=Mesh(name,vertices,faces,role,group,normals); objects.append(m); return m

def cylinder(name,center,r,depth,role='steel',group='Body',axis='X',verts=40):
    k={'X':0,'Y':1,'Z':2}[axis]; u=(k+1)%3; v=(k+2)%3
    vs=[]; ns=[]; faces=[]; c=np.array(center)
    # Rounded rim edges, six rings; flat cap vertices stay separate from smooth barrel.
    bevel=min(.008,depth*.22,r*.05)
    rings=[(-depth/2,r-bevel),(-depth/2+bevel*.3,r-bevel*.3),(-depth/2+bevel,r),(depth/2-bevel,r),(depth/2-bevel*.3,r-bevel*.3),(depth/2,r-bevel)]
    for x,rad in rings:
        for i in range(verts):
            a=i*2*math.pi/verts;p=c.copy();p[k]+=x;p[u]+=rad*math.cos(a);p[v]+=rad*math.sin(a);vs.append(p)
    for j in range(len(rings)-1):
        for i in range(verts):
            a=j*verts+i;b=j*verts+(i+1)%verts;faces.append((a,b,b+verts,a+verts))
    faces+=[tuple(range(verts-1,-1,-1)),tuple((len(rings)-1)*verts+i for i in range(verts))]
    return obj(name,vs,faces,role,group)

def export(out,kind,info,obs):
    buckets=defaultdict(list)
    for ob in obs:
        if ob.part!='Preview wheels': buckets[(ob.part,ob.role)].append(ob)
    def string(f,s):
        b=s.encode();f.write(struct.pack('<i',len(b)));f.write(b)
    totalv=totalt=0;path=out/(kind+'.bytes')
    with path.open('wb') as f:
        f.write(b'COV2');f.write(struct.pack('<5f',*[info[k] for k in ['ground','halfLength','wheelRadius','track','tyreWidth']]))
        f.write(struct.pack('<6f',*(info['cabin'] or [0]*6)));f.write(struct.pack('<i',len(buckets)))
        for (part,role),meshes in buckets.items():
            vs=[];ns=[];ts=[];uvs=[];offset=0
            for m in meshes:
                vs.extend(m.v);ns.extend(m.n);ts.extend(m.f+offset);offset+=len(m.v)
                if role=='plate':
                    lo=m.v.min(axis=0); span=np.maximum(m.v.max(axis=0)-lo,1e-6)
                    uvs.extend(np.column_stack(((m.v[:,0]-lo[0])/span[0],(m.v[:,2]-lo[2])/span[2])))
                else:
                    dominant=np.argmax(abs(m.n),axis=1)
                    uvs.extend(np.column_stack((np.where(dominant==0,m.v[:,1],m.v[:,0]),np.where(dominant==2,m.v[:,1],m.v[:,2]))))
            vs=np.array(vs); ns=np.array(ns); ts=np.array(ts)
            uv=np.array(uvs)
            vs=vs[:,[0,2,1]];vs[:,1]+=info['ground'];ns=ns[:,[0,2,1]];ts=ts[:,[0,2,1]]
            string(f,part);string(f,role);f.write(struct.pack('<ii',len(vs),ts.size))
            f.write(np.column_stack((vs,ns,uv)).astype('<f4').tobytes());f.write(ts.astype('<i4').tobytes())
            totalv+=len(vs);totalt+=len(ts)
    return dict(file=path.name,vertices=totalv,triangles=totalt,draws=len(buckets),bytes=path.stat().st_size)

def render(obs,path,eye=(6.2,7.7,3.6),target=(0,0,.65),width=1200,height=800):
    eye=np.array(eye); target=np.array(target); forward=target-eye;forward/=np.linalg.norm(forward)
    right=np.cross(forward,[0,0,1]);right/=np.linalg.norm(right);up=np.cross(right,forward)
    basis=np.array([right,up,forward]);focal=width*1.12
    yy,xx=np.mgrid[:height,:width]
    # A floor and soft contact shadow in world space, shaded by a neutral studio environment.
    rays=(xx-width/2)[...,None]*right-(yy-height/2)[...,None]*up+focal*forward
    t=-eye[2]/np.minimum(-1e-5,rays[:,:,2]);ground=eye+rays*t[:,:,None]
    shadow=np.exp(-(ground[:,:,0]/1.2)**4-(ground[:,:,1]/2.45)**4)*.72
    col=np.full((height,width,3),.13)*(1-shadow[:,:,None])
    depth=np.full((height,width),np.inf)
    lights=[(np.array([.35,.5,1.]),2.0),(np.array([-1,-.4,.8]),1.0)]
    for l,_ in lights:l/=np.linalg.norm(l)
    for ob in obs:
        vs=ob.v;ns=ob.n;rel=(vs-eye)@basis.T
        screen=np.column_stack((width/2+focal*rel[:,0]/rel[:,2],height/2-focal*rel[:,1]/rel[:,2]))
        mat=materials[ob.role];base=np.array(mat['color']); metal=mat['metal']; rough=mat['rough']
        for face in ob.f:
            p=screen[face];zs=rel[face,2]
            if np.min(zs)<=0:continue
            mn=np.maximum(np.floor(p.min(axis=0)).astype(int),[0,0]);mx=np.minimum(np.ceil(p.max(axis=0)).astype(int),[width-1,height-1])
            if np.any(mx<mn):continue
            x0,y0=mn;x1,y1=mx
            x,y=np.meshgrid(np.arange(x0,x1+1)+.5,np.arange(y0,y1+1)+.5)
            den=(p[1,1]-p[2,1])*(p[0,0]-p[2,0])+(p[2,0]-p[1,0])*(p[0,1]-p[2,1])
            if abs(den)<1e-8:continue
            a=((p[1,1]-p[2,1])*(x-p[2,0])+(p[2,0]-p[1,0])*(y-p[2,1]))/den
            b=((p[2,1]-p[0,1])*(x-p[2,0])+(p[0,0]-p[2,0])*(y-p[2,1]))/den;c=1-a-b
            bary=np.stack((a,b,c),axis=-1)/zs;z=1/np.sum(bary,axis=-1);bary*=z[...,None]
            mask=(a>=0)&(b>=0)&(c>=0)&(z<depth[y0:y1+1,x0:x1+1])
            if not np.any(mask):continue
            normal=bary@ns[face];normal/=np.maximum(1e-10,np.linalg.norm(normal,axis=-1))[...,None]
            point=bary@vs[face];view=eye-point;view/=np.maximum(1e-10,np.linalg.norm(view,axis=-1))[...,None]
            # Show both sides of glass/sheet in inspection; exported Unity winding is checked separately.
            nv=np.sum(normal*view,axis=-1);normal=np.where((nv<0)[...,None],-normal,normal);nv=abs(nv)
            color=base[None,None,:]*(.20+.15*np.maximum(0,normal[:,:,2,None]))
            for light,power in lights:
                nd=np.maximum(0,np.sum(normal*light,axis=-1))
                half=view+light;half/=np.linalg.norm(half,axis=-1)[...,None]
                nh=np.maximum(0,np.sum(normal*half,axis=-1))
                spec=nh**max(12,2/(rough*rough))
                color+=base[None,None,:]*nd[...,None]*power*(1-metal*.65)
                color+=spec[...,None]*power*(.15+metal*.7)
            reflected=2*nv[...,None]*normal-view
            # Broad rectangular studio-card reflections make continuity/normal defects obvious.
            card=np.exp(-((reflected[:,:,0]-.15)/.3)**8-((reflected[:,:,2]-.8)/.25)**8)
            fresnel=.045+(1-nv)**5
            color+=card[...,None]*(.4+metal*.7)*(.45+fresnel[...,None])
            color+=fresnel[...,None]*np.maximum(0,reflected[:,:,2,None])*.22
            # Simple filmic rolloff, linear->display transform at end.
            col[y0:y1+1,x0:x1+1][mask]=color[mask];depth[y0:y1+1,x0:x1+1][mask]=z[mask]
    mapped=np.clip((col*(2.51*col+.03))/(col*(2.43*col+.59)+.14),0,1)**(1/2.2)
    im=Image.fromarray((mapped*255).astype('uint8'));im.save(path);return im

def contact_sheet(collections,path):
    sheet=Image.new('RGB',(1400,1200),(35,39,44));draw=ImageDraw.Draw(sheet)
    for i,(name,obs) in enumerate(collections.items()):
        large=name in ['Bus','Tanker'];d=1.65 if large else 1
        im=render(obs,path.with_name('_'+name+'.png'),tuple(d*x for x in (6.2,7.7,3.6)),(0,0,1 if large else .65),700,300)
        x=(i%2)*700;y=(i//2)*300;sheet.paste(im,(x,y));draw.text((x+22,y+22),name,fill='white')
    sheet.save(path)
