"""Original Crashout vehicle assets. Run with Blender --background --python this_file.

Authoring coordinates: X across, Y forward, Z up, metres. The .bytes exporter
converts to Unity's X-right/Y-up/Z-forward, reversing triangle winding.
No third-party models, textures, or Blender add-ons required.
"""
import math, os, struct, json, sys
PORTABLE = "--portable" in sys.argv
if PORTABLE:
    from portable_mesh import Vector
else:
    import bpy
    from mathutils import Vector
from pathlib import Path
from collections import defaultdict

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Unity/Assets/Crashout/Resources/Vehicles'
ART = ROOT / 'Art/Vehicles'
OUT.mkdir(parents=True, exist_ok=True); ART.mkdir(parents=True, exist_ok=True)
pi = math.pi
def lerp(a,b,t): return a+(b-a)*t
def clamp(v,a=0,b=1): return max(a,min(b,v))
def smooth(t): return clamp(t)**2*(3-2*clamp(t))
def key(z, points):
    if z <= points[0][0]: return points[0][1]
    for (a,x),(b,y) in zip(points,points[1:]):
        if z<=b: return lerp(x,y,smooth((z-a)/(b-a)))
    return points[-1][1]
def xyz(x,h,z): return (x,z,h)

if not PORTABLE:
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
materials={}
def material(name,c,metal=0,rough=.35,coat=0):
    if PORTABLE:
        materials[name] = dict(color=c,metal=metal,rough=rough,coat=coat); return materials[name]
    m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*c,1); p.inputs['Metallic'].default_value=metal
    p.inputs['Roughness'].default_value=rough; p.inputs['Coat Weight'].default_value=coat
    p.inputs['Coat Roughness'].default_value=.12
    materials[name]=m; return m
material('paint',(.38,.075,.012),.65,.27,1)
material('framePaint',(.38,.075,.012),.65,.27,1)
material('glass',(.018,.028,.035),.25,.12,.8)
material('trim',(.012,.014,.017),.08,.38)
material('underbody',(.025,.028,.032),.5,.47)
material('interior',(.026,.021,.018),0,.7)
material('chrome',(.55,.58,.62),1,.2)
material('steel',(.37,.4,.44),.85,.31)
material('headlight',(.75,.84,.92),.4,.15)
material('tail',(.3,.008,.012),.35,.2)
material('indicator',(.75,.26,.015),.2,.23)
material('plate',(.67,.7,.72),.1,.5)
material('rubber',(.014,.016,.018),0,.72)

objects=[]
def obj(name,verts,faces,role='paint',group='Body',thickness=0):
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    ob=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(ob)
    ob.data.materials.append(materials[role]); ob['role']=role; ob['part']=group
    for p in mesh.polygons: p.use_smooth=True
    if thickness:
        mod=ob.modifiers.new('Sheet gauge','SOLIDIFY'); mod.thickness=thickness; mod.offset=-1
    objects.append(ob); return ob

def surface(name,fn,rows=25,cols=13,role='paint',group='Body',thick=.0025,out=(0,0,1)):
    vs=[fn(i/(rows-1),j/(cols-1)) for i in range(rows) for j in range(cols)]
    faces=[]
    for i in range(rows-1):
        for j in range(cols-1):
            a=i*cols+j; f=(a,a+cols,a+cols+1,a+1)
            n=(Vector(vs[f[1]])-Vector(vs[f[0]])).cross(Vector(vs[f[2]])-Vector(vs[f[0]]))
            direction=out(vs[f[0]]) if callable(out) else out
            if n.dot(Vector(direction))<0: f=f[::-1]
            faces.append(f)
    return obj(name,vs,faces,role,group,thick)

def box(name,center,size,role='trim',group='Body',bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center); o=bpy.context.object; o.name=name
    o.dimensions=size; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(materials[role]); o['role']=role; o['part']=group; objects.append(o)
    if bevel:
        b=o.modifiers.new('Manufactured edges','BEVEL'); b.width=bevel; b.segments=3
        n=o.modifiers.new('Face normals','WEIGHTED_NORMAL'); n.keep_sharp=True
    return o

def tube(name,points,r=.012,role='trim',group='Body',sides=8):
    verts=[]
    for i,p in enumerate(points):
        tangent=Vector(points[min(len(points)-1,i+1)])-Vector(points[max(0,i-1)])
        tangent.normalize(); basis=tangent.cross(Vector((0,0,1)))
        if basis.length<.01: basis=tangent.cross(Vector((1,0,0)))
        basis.normalize(); other=tangent.cross(basis).normalized()
        for j in range(sides): verts.append(Vector(p)+r*(math.cos(j*2*pi/sides)*basis+math.sin(j*2*pi/sides)*other))
    faces=[]
    for i in range(len(points)-1):
        for j in range(sides):
            a=i*sides+j; b=i*sides+(j+1)%sides; faces.append((a,b,b+sides,a+sides))
    return obj(name,verts,faces,role,group)

def cylinder(name,center,r,depth,role='steel',group='Body',axis='X',verts=40):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=depth,location=center)
    o=bpy.context.object; o.name=name
    if axis=='X': o.rotation_euler[1]=pi/2
    elif axis=='Y': o.rotation_euler[0]=pi/2
    o.data.materials.append(materials[role]); o['role']=role; o['part']=group; objects.append(o)
    b=o.modifiers.new('Edge radii','BEVEL'); b.width=.008; b.segments=2
    n=o.modifiers.new('Normals','WEIGHTED_NORMAL'); n.keep_sharp=True
    return o

if PORTABLE:
    import portable_mesh as portable
    portable.objects=objects; portable.materials=materials
    obj=portable.obj; box=portable.box; cylinder=portable.cylinder

DESIGNS={
 'Player':dict(L=4.25,W=1.86,H=1.23,R=.31,A=1.3,ground=-.405,cabin=(-1.38,-.64,.24,1.02),belt=.73,sport=True),
 'Sedan':dict(L=4.35,W=1.82,H=1.42,R=.31,A=1.3,ground=-.29,cabin=(-1.45,-.8,.37,1.03),belt=.82),
 'Hatch':dict(L=3.85,W=1.74,H=1.47,R=.30,A=1.17,ground=-.29,cabin=(-1.73,-1.28,.28,.98),belt=.84),
 'Van':dict(L=4.65,W=1.95,H=2.1,R=.34,A=1.42,ground=-.35,cabin=(-2.22,-2.12,.85,1.55),belt=1.04,van=True),
 'Pickup':dict(L=4.85,W=1.98,H=1.78,R=.36,A=1.5,ground=-.29,cabin=(-.5,-.44,.66,1.22),belt=1.06,bed=True),
}

def passenger(kind,d):
    L,W,H,R,A=d['L'],d['W'],d['H'],d['R'],d['A']; half=L/2; w=W/2
    rb,rt,ft,fb=d['cabin']; belt=d['belt']; sport=d.get('sport'); van=d.get('van'); bed=d.get('bed')
    roofw=.78 if not van else .91
    def width(z):
        return w*key(z,[(-half,.84),(-half+.25,.96),(-A,1),(-.25,.965),(A,1),(half-.25,.95),(half,.83)])
    def deck(z):
        return key(z,[(-half,belt-.12),(-half+.3,belt),(-A,belt+.015),(fb,belt),(A,belt-.015),(half-.25,belt-.10),(half,belt-.16)])
    def low(z):
        b=.18 if not bed else .26
        for axle in [-A,A]:
            q=abs(z-axle); arch=R+.045
            if q<arch: b=max(b,R+math.sqrt(arch*arch-q*q))
        return min(b,deck(z)-.025)
    def sidepoint(sign,z,v):
        h=lerp(low(z),deck(z),v)
        # Continuous sill -> concave door -> crisp shoulder -> rolled deck edge.
        u=clamp((h-.18)/max(.2,deck(z)-.18))
        x=width(z)*key(u,[(0,.925),(.18,.975),(.5,.966),(.78,1),(.86,.998),(1,.9)])
        return xyz(sign*x,h,z)
    # Panel edges use the SAME sampled surface. Six mm reveal, no exposed body holes.
    mid=-.22 if not sport else -.85
    rearDoor=max(rb+.12,-A+.35)
    bounds=[-half,rearDoor,mid,fb-.08,half]
    if sport: bounds=[-half,-.88,fb-.08,half]
    if bed: bounds=[-half,rb,fb-.08,half]
    if van: bounds=[-half,-.8,.35,fb-.08,half]
    for s in [-1,1]:
        for k,(za,zb) in enumerate(zip(bounds,bounds[1:])):
            group='Body'
            if k>0 and k<len(bounds)-2: group=('Left ' if s<0 else 'Right ')+('front door' if k==len(bounds)-3 else 'rear door')
            a=za+(.003 if k else 0); b=zb-(.003 if k<len(bounds)-2 else 0)
            surface(group+' outer',lambda u,v,s=s,a=a,b=b:sidepoint(s,lerp(a,b,u),v),max(12,int((b-a)*30)),13,group=group,out=(s,0,.1))
            if group!='Body':
                hz=a+.16
                box('Handle pocket',xyz(s*(width(hz)*.989+.002),belt-.1,hz),(.012,.17,.037),'trim',group,.014)
                box('Handle',xyz(s*(width(hz)*.995+.012),belt-.095,hz),(.018,.135,.022),'chrome',group,.009)
        # Inner wheelhouses + rolled edge strip. Inner liners remain when a wing is crushed.
        for axle in [-A,A]:
            arch=R+.045
            surface('Wheelhouse',lambda u,v,s=s,axle=axle:xyz(s*lerp(w-.31,w-.035,v),R+math.sin(u*pi)*arch,axle+math.cos(u*pi)*arch),33,4,'underbody',out=(-s,0,0))
            pts=[xyz(s*(width(axle+math.cos(i*pi/40)*arch)*.992),R+math.sin(i*pi/40)*arch,axle+math.cos(i*pi/40)*arch) for i in range(41)]
            tube('Rolled wheel arch',pts,.009,'paint',sides=6)
        tube('Rocker seam',[xyz(s*w*.95,.205,z) for z in [(-A+R+.03),0,(A-R-.03)]],.017,'trim')
    # Decks close exactly against the shoulder, with pressed longitudinal character creases.
    def top(z,x):
        crown=.024*(1-x*x)
        crease=.014*math.exp(-((abs(x)-.62)/.09)**2)*math.sin(clamp((z-fb)/max(.1,half-fb))*pi)
        return xyz(x*width(z)*.9,deck(z)+crown+crease,z)
    surface('Hood',lambda u,v:top(lerp(fb+.004,half,u),lerp(-1,1,v)),33,25,group='Hood')
    if not bed:
        surface('Trunk lid',lambda u,v:top(lerp(-half,rb-.004,u),lerp(-1,1,v)),max(10,int((rb+half)*35)),25,group='Trunk lid')
    def roofheight(z):
        if z<rt: return lerp(deck(z),H-.028,smooth((z-rb)/(rt-rb)))
        if z>ft: return lerp(H-.028,deck(z),smooth((z-ft)/(fb-ft)))
        return H-.028+.028*math.sin(pi*clamp((z-rt)/(ft-rt)))
    def roofwidth(z):
        f=clamp((roofheight(z)-deck(z))/(H-deck(z)))
        return width(z)*lerp(.9,roofw,f)
    def cabinpoint(z,x,lift=0): return xyz(x*roofwidth(z),roofheight(z)+.025*(1-x*x)+lift,z)
    # Roof has compound curvature; glazing is inset inside continuous metal aperture surrounds.
    surface('Roof outer',lambda u,v:cabinpoint(lerp(rt,ft,u),lerp(-1,1,v)),31,25,group='Roof panel')
    for front in [False,True]:
        a,b=(ft,fb) if front else (rb,rt)
        margin=min(.045,(b-a)*.15)
        if van and not front:
            surface('Rear cargo door',lambda u,v,a=a,b=b:cabinpoint(lerp(a,b,u),lerp(-1,1,v)),19,21,'paint',out=(0,-1,0))
        else:
            # Actual aperture: no painted plate hidden under the glass.
            for u0,u1,v0,v1 in [(0,margin/(b-a),0,1),(1-margin/(b-a),1,0,1),(margin/(b-a),1-margin/(b-a),0,.034),(margin/(b-a),1-margin/(b-a),.966,1)]:
                surface('Windscreen aperture',lambda u,v,u0=u0,u1=u1,v0=v0,v1=v1,a=a,b=b:cabinpoint(lerp(a,b,lerp(u0,u1,u)),lerp(-1,1,lerp(v0,v1,v))),3 if u1-u0<.2 else 19,21 if v1-v0>.2 else 3,'framePaint',thick=.002,out=(0,1 if front else -1,1))
        surface('Windscreen' if front else 'Rear glass',lambda u,v,a=a,b=b:cabinpoint(lerp(a+margin,b-margin,u),lerp(-.935,.935,v),.006),19,21,'paint' if van and not front else 'glass',thick=0,out=(0,1 if front else -1,1))
        for s in [-1,1]: tube('Glass rubber',[cabinpoint(lerp(a+.045,b-.045,i/25),s*.95,.007) for i in range(26)],.008,'trim')
    split=.35 if van else -.27 if not sport else -.74
    for s in [-1,1]:
        def cp(z,v):
            return xyz(s*lerp(width(z)*.9,roofwidth(z),v),lerp(deck(z),roofheight(z),v),z)
        spans=[(rb,split,'Body' if sport or bed else ('Left rear door' if s<0 else 'Right rear door')),(split,fb,'Left front door' if s<0 else 'Right front door')]
        for a,b,group in spans:
            if van and b==split:
                surface('Cargo side panel',lambda u,v,a=a,b=b:cp(lerp(a,b,u),v),25,15,'paint',group,out=(s,0,.2))
            else:
                m=min(.061/(b-a),.2)
                for u0,u1,v0,v1 in [(0,m,0,1),(1-m,1,0,1),(m,1-m,0,.09),(m,1-m,.88,1)]:
                    surface('Door aperture',lambda u,v,u0=u0,u1=u1,v0=v0,v1=v1,a=a,b=b:cp(lerp(a,b,lerp(u0,u1,u)),lerp(v0,v1,v)),3 if u1-u0<.25 else 23,7 if v1-v0>.2 else 3,'framePaint',group,out=(s,0,.2))
            # Outer black ceramic border, then smaller glass. Insets keep pillars substantial.
            for margin,role,offset in [(.037,'trim',.002),(.061,'glass',.004)]:
                def win(u,v,a=a,b=b,margin=margin,offset=offset):
                    z=lerp(a+margin,b-margin,u); p=Vector(cp(z,lerp(.09,.88,v))); p.x+=s*offset; return p
                window_role = 'paint' if van and b==split else role
                surface('Side '+role,win,23,7,window_role,group,0,out=(s,0,.2))
        # Chrome beltline, window seal, mirrors with real stalks and inset reflective glass.
        tube('Beltline brightwork',[xyz(s*(width(z)*.903),deck(z)+.009,z) for z in [lerp(rb,fb,i/40) for i in range(41)]],.007,'chrome')
        mz=fb-.19; mh=deck(mz)+.13; group='Left mirror' if s<0 else 'Right mirror'
        tube('Mirror stalk',[xyz(s*w*.9,mh-.04,mz),xyz(s*(w+.065),mh,mz)],.025,'trim',group)
        box('Mirror shell',xyz(s*(w+.09),mh,mz),(.19,.235,.105),'framePaint',group,.045)
        box('Mirror inset',xyz(s*(w+.09),mh,mz-.118),(.145,.008,.071),'chrome',group,.018)
    # Bumper fascia wraps both corners. Lamp nests and grilles follow the fascia curvature.
    for end in [-1,1]:
        group='Front bumper' if end==1 else 'Rear bumper'
        def face(x,h,offset=0):
            z=end*(half-.005-.10*abs(x)**5+offset)
            return xyz(x*width(end*half),h,z)
        surface(group,lambda u,v:face(lerp(-1,1,v),lerp(.21,deck(end*half),u)),15,37,group=group,out=(0,end,0))
        box('Lower valance',xyz(0,.23,end*(half-.03)),(W*.78,.15,.11),'trim',group,.035)
        box('Plate surround',xyz(0,.50 if sport and end==1 else .41,end*(half+.006)),(.47,.02,.14),'trim',group,.015)
        box('Number plate',xyz(0,.50 if sport and end==1 else .41,end*(half+.02)),(.42,.008,.105),'plate',group,.006)
        for s in [-1,1]:
            # Tapered swept housings rather than glowing rectangular blocks.
            def lamp(u,v,s=s,end=end):
                x=s*lerp(.42,.91,u); h=deck(end*half)-.055- .035*u + (v-.5)*lerp(.115,.075,u)
                return face(x,h,.013)
            surface('Lamp housing',lamp,15,5,'trim',group,0,out=(0,end,0))
            surface('Lamp lens',lambda u,v:lamp(lerp(.06,.94,u),lerp(.14,.86,v)),13,5,'headlight' if end==1 else 'tail',group,0,out=(0,end,0))
            if end==1:
                for i in range(2):
                    x=s*(.52+.15*i)*width(half); h=deck(half)-.066
                    cylinder('Projector bezel',xyz(x,h,half+.018),.034,.014,'chrome',group,'Y',20)
                    cylinder('Projector lens',xyz(x,h,half+.028),.025,.008,'headlight',group,'Y',20)
                box('Side intake',xyz(s*w*.69,.33,half-.012),(.24,.04,.13),'trim',group,.035)
                for i in range(3): box('Intake vane',xyz(s*w*.69,.29+i*.033,half+.012),(.2,.012,.008),'underbody',group,.003)
        if end==1:
            grille_y = .37 if sport else .64 if van or bed else .53
            grille_w = 1.05 if sport else .94 if van or bed else .64
            grille_h = .18 if sport else .28 if van or bed else .14
            box('Radiator mouth',xyz(0,grille_y,half+.008),(grille_w,.027,grille_h),'trim',group,.035)
            if van or bed:
                for i in range(4): box('Grille fin',xyz(0,grille_y-grille_h*.35+i*grille_h*.23,half+.024),(grille_w*.92,.012,.014),'chrome',group,.004)
            else:
                for i in range(15): box('Grille fin',xyz(-grille_w*.44+i*grille_w*.88/14,grille_y,half+.024),(.006,.012,grille_h*.75),'underbody',group,.002)
        else:
            for s in [-1,1]:
                cylinder('Exhaust rolled tip',xyz(s*.61,.23,-half-.035),.052,.1,'chrome',group,'Y')
                cylinder('Exhaust opening',xyz(s*.61,.23,-half-.09),.041,.003,'underbody',group,'Y')
    # Structure and mechanical assemblies stay visible behind torn sheet metal.
    box('Floor pan',xyz(0,.25,0),(W*.81,L-.45,.1),'underbody',bevel=.035)
    for s in [-1,1]:
        box('Chassis rail',xyz(s*.48,.22,0),(.105,L-.4,.13),'steel',bevel=.015)
        box('Front bucket',xyz(s*.39,.55,.03),(.48,.5,.15),'interior',bevel=.065)
        seat=box('Seat back',xyz(s*.39,.79,-.22),(.46,.15,.5),'interior',bevel=.065); seat.rotation_euler[0]=-.16
        box('Head restraint',xyz(s*.39,1.05,-.25),(.26,.12,.16),'interior',bevel=.045)
    box('Dashboard',xyz(0,belt-.07,fb-.23),(W*.79,.28,.16),'interior',bevel=.07)
    tube('Steering wheel',[xyz(-.39+.15*math.cos(i*2*pi/32),belt+.02+.15*math.sin(i*2*pi/32),fb-.42) for i in range(33)],.018,'trim')
    box('Engine block',xyz(0,.42,A),(.62,.6,.25),'underbody',bevel=.055)
    for i in range(6): box('Engine rib',xyz(-.25+i*.1,.558,A),(.035,.52,.025),'steel',bevel=.008)
    box('Radiator',xyz(0,.5,half-.16),(.96,.1,.28),'underbody',bevel=.02)
    if bed:
        length=rb+half
        box('Bed floor',xyz(0,.61,(-half+rb)/2),(W*.82,length,.035),'trim',bevel=.015)
        for s in [-1,1]: box('Bed inner wall',xyz(s*w*.85,.86,(-half+rb)/2),(.035,length,.48),'paint',bevel=.012)
        box('Tailgate inner',xyz(0,.85,-half+.09),(W*.84,.03,.46),'paint','Tailgate',.02)
        for i in range(9): box('Bed rib',xyz(-.7+i*.175,.645,(-half+rb)/2),(.028,length-.09,.016),'underbody',bevel=.006)
    # Store cabin collider in road-space; runtime applies root height offset.
    return dict(ground=d['ground'],halfLength=half,wheelRadius=R,track=w-.12,tyreWidth=.23 if sport else .21,
                cabin=[0,(belt+H)/2,(rt+ft)/2,W*roofw,.9*(H-belt),ft-rt+.2])

def heavy(kind):
    bus=kind=='Bus'; L=7.4 if bus else 8; w=1.15; H=2.6 if bus else 2.9
    if bus:
        # Coach shell: curved tumblehome, radiused upper corners and recessed full glazing band.
        for s in [-1,1]:
            def wall(u,v):
                z=lerp(-L/2,L/2,u); h=lerp(.3,2.48,v)
                for axle in [-2.4,2.4]:
                    q=abs(z-axle)
                    if q<.56: h=lerp(max(.3,.5+math.sqrt(.56**2-q*q)),2.48,v)
                return xyz(s*(w-.11*smooth((h-2.05)/.43)),h,z)
            surface('Coach side',wall,101,19,out=(s,0,0))
            for i in range(7):
                z=-2.95+i*.89
                box('Window gasket',xyz(s*1.137,1.9,z),(.018,.8,.83),'trim',bevel=.045)
                box('Coach glass',xyz(s*1.151,1.91,z),(.012,.744,.76),'glass',bevel=.04)
                box('Luggage bay seam',xyz(s*1.151,.72,z),(.009,.78,.015),'trim',bevel=.003)
                box('Bay latch',xyz(s*1.163,.84,z),(.015,.11,.026),'chrome',bevel=.008)
            for axle in [-2.4,2.4]:
                tube('Coach wheel opening',[xyz(s*1.155,.5+math.sin(i*pi/32)*.56,axle+math.cos(i*pi/32)*.56) for i in range(33)],.02,'trim')
            tube('Coach belt trim',[xyz(s*1.155,1.35,-3.5),xyz(s*1.155,1.35,3.5)],.014,'chrome')
        surface('Coach roof',lambda u,v:xyz(lerp(-1.04,1.04,v),2.49+.11*(1-(2*v-1)**2),lerp(-3.7,3.7,u)),65,21,group='Roof panel')
        for end in [-1,1]:
            box('Coach end',xyz(0,1.36,end*3.62),(2.24,.16,2.18),'paint',bevel=.12)
            box('Coach windscreen',xyz(0,1.95,end*3.713),(1.99,.024,1.02),'glass',bevel=.09)
            box('Coach bumper',xyz(0,.39,end*3.69),(2.24,.19,.22),'paint','Front bumper' if end==1 else 'Rear bumper',.06)
            for s in [-1,1]: box('Coach lamps',xyz(s*.82,.68,end*3.72),(.31,.03,.15),'headlight' if end>0 else 'tail',bevel=.04)
        for i in range(13): box('Engine cooling slat',xyz(0,.84+i*.033,-3.72),(1.5,.022,.012),'trim',bevel=.002)
        box('Roof climate unit',xyz(0,2.63,-.6),(1.25,2.2,.16),'underbody',bevel=.075)
    else:
        # Pressed cab, a smooth elliptical stainless tank, straps and a proper ladder/chassis.
        for s in [-1,1]:
            surface('Truck cab side',lambda u,v:xyz(s*(1.12-.085*smooth((lerp(.4,2.45,v)-1.9)/.55)),lerp(max(.4,.5+math.sqrt(max(0,.57**2-(lerp(1.65,4,u)-3)**2))) if abs(lerp(1.65,4,u)-3)<.57 else .4,2.45,v),lerp(1.65,4,u)),55,19,out=(s,0,0))
            box('Cab window seal',xyz(s*1.135,1.96,2.89),(.026,1.34,.71),'trim',bevel=.075)
            box('Cab side glass',xyz(s*1.153,1.97,2.89),(.016,1.24,.61),'glass',bevel=.06)
            box('Truck mirror',xyz(s*1.29,1.99,3.54),(.13,.12,.35),'trim','Left mirror' if s<0 else 'Right mirror',.035)
            tube('Mirror bracket',[xyz(s*1.08,2.13,3.44),xyz(s*1.3,2.13,3.54)],.022,'steel')
            for j in range(2): box('Access step',xyz(s*1.13,.48+j*.22,2.0),(.16,.6,.055),'steel',bevel=.012)
        box('Cab roof',xyz(0,2.45,2.8),(2.13,2.4,.12),'paint','Roof panel',.055)
        box('Cab front',xyz(0,1.4,3.94),(2.18,.12,2.02),'paint',bevel=.065)
        box('Truck windshield',xyz(0,2.03,4.007),(1.95,.018,.68),'glass',bevel=.07)
        box('Truck grille',xyz(0,1.24,4.016),(1.34,.03,.47),'trim',bevel=.025)
        for i in range(8): box('Grille ribs',xyz(0,1.05+i*.05,4.04),(1.29,.018,.018),'chrome',bevel=.004)
        box('Truck bumper',xyz(0,.48,3.97),(2.28,.18,.25),'chrome','Front bumper',.045)
        for s in [-1,1]: box('Truck headlamp',xyz(s*.87,.79,4.02),(.32,.04,.22),'headlight',bevel=.03)
        # Tank axis forward; compound end caps, 64 sides and axial loops suitable for dents.
        def tank(u,v):
            z=lerp(-3.8,1.48,u); cap=math.sqrt(max(.002,1-(max(0,abs((z+1.16)/2.64)-.83)/.17)**2))
            a=v*2*pi
            return xyz(math.cos(a)*1.06*cap,1.88+math.sin(a)*.94*cap,z)
        surface('Stainless vessel',tank,77,65,'steel',thick=.004,out=lambda p:(p[0],0,p[2]-1.88))
        # Explicit recalc gives proper cylindrical winding (outward varies around circumference).
        if not PORTABLE:
            ob=objects[-1]; bpy.context.view_layer.objects.active=ob; ob.select_set(True)
            bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT'); ob.select_set(False)
        for z in [-2.9,-.9,1.02]:
            tube('Tank retaining band',[xyz(math.cos(i*2*pi/64)*1.075,1.88+math.sin(i*2*pi/64)*.952,z) for i in range(65)],.026,'chrome',sides=6)
        for z in [-2.1,.3]: cylinder('Tank service hatch',xyz(0,2.84,z),.23,.08,'steel',axis='Z')
        for s in [-1,1]:
            tube('Ladder upright',[xyz(s*.24,.6,-3.85),xyz(s*.24,2.56,-3.85)],.022,'steel')
            box('Rear mudguard',xyz(s*.96,.99,-2.3),(.43,2.6,.06),'trim',bevel=.02)
        for i in range(8): tube('Ladder rung',[xyz(-.24,.68+i*.25,-3.89),xyz(.24,.68+i*.25,-3.89)],.018,'steel')
        box('Rear underride bar',xyz(0,.4,-3.9),(2.2,.12,.14),'steel','Rear bumper',.025)
    for s in [-1,1]: box('Heavy chassis rail',xyz(s*.66,.54,0),(.14,L-.3,.23),'underbody',bevel=.025)
    return dict(ground=-1.15 if bus else -1.25,halfLength=L/2,wheelRadius=.5,track=1.,tyreWidth=.32,cabin=[])

def preview_wheels(info,kind):
    r=info['wheelRadius']; track=info['track']; width=info['tyreWidth']
    axles=[DESIGNS[kind]['A'],-DESIGNS[kind]['A']] if kind in DESIGNS else ([2.4,-2.4] if kind=='Bus' else [3,-1.6,-3])
    for z in axles:
        for s in [-1,1]:
            group='Preview wheels'
            cylinder('Tyre',xyz(s*track,r,z),r,width,'rubber',group,verts=64)
            cylinder('Wheel barrel',xyz(s*(track+width*.5+.002),r,z),r*.71,.012,'trim',group,verts=48)
            cylinder('Brake rotor',xyz(s*(track+width*.5+.009),r,z),r*.57,.008,'steel',group,verts=48)
            # Slim Y-spokes and a rolled rim lip, no filled silver disc.
            pts=[xyz(s*(track+width*.5+.018),r+math.cos(i*2*pi/64)*r*.73,z+math.sin(i*2*pi/64)*r*.73) for i in range(65)]
            tube('Rim lip',pts,.012,'chrome',group)
            for i in range(5):
                a=i*2*pi/5
                for delta in [-.12,.12]:
                    tube('Spoke',[xyz(s*(track+width*.5+.027),r+math.cos(a)*r*.15,z+math.sin(a)*r*.15),xyz(s*(track+width*.5+.024),r+math.cos(a+delta)*r*.69,z+math.sin(a+delta)*r*.69)],.014,'chrome',group)
            cylinder('Hub',xyz(s*(track+width*.5+.03),r,z),r*.16,.018,'chrome',group,verts=24)

def export(kind,info,car_objects):
    if PORTABLE: return portable.export(OUT,kind,info,car_objects)
    buckets=defaultdict(list); deps=bpy.context.evaluated_depsgraph_get()
    for o in car_objects:
        if o.get('part')=='Preview wheels': continue
        buckets[(o['part'],o['role'])].append(o)
    path=OUT/(kind+'.bytes'); totalv=totalt=0
    def string(f,s):
        b=s.encode('utf8'); f.write(struct.pack('<i',len(b))); f.write(b)
    with path.open('wb') as f:
        f.write(b'COV2'); f.write(struct.pack('<5f',*[info[k] for k in ['ground','halfLength','wheelRadius','track','tyreWidth']]))
        cabin=info['cabin'] or [0]*6; f.write(struct.pack('<6f',*cabin)); f.write(struct.pack('<i',len(buckets)))
        for (part,role),obs in buckets.items():
            vertices=[]; normals=[]; uv=[]; indices=[]; lookup={}
            for ob in obs:
                ev=ob.evaluated_get(deps); me=ev.to_mesh(); me.calc_loop_triangles(); m=ev.matrix_world; nm=m.to_3x3().inverted().transposed()
                # Preserve split normals at folded/thickness edges, weld equal corners for compact assets.
                cn=me.corner_normals
                for tri in me.loop_triangles:
                    face=[]
                    for li in tri.loops:
                        loop=me.loops[li]; p=m@me.vertices[loop.vertex_index].co; n=(nm@cn[li].vector).normalized()
                        v=(p.x,p.z+info['ground'],p.y); normal=(n.x,n.z,n.y)
                        k=tuple(round(x,6) for x in (*v,*normal))
                        if k not in lookup:
                            lookup[k]=len(vertices); vertices.append(v); normals.append(normal)
                            # Stable metre UVs, fine-scale authored details are geometric.
                            if role=='plate':
                                uv.append(((p.x-m.translation.x)/max(.001,ob.dimensions.x)+.5,(p.z-m.translation.z)/max(.001,ob.dimensions.z)+.5))
                            elif abs(n.x)>abs(n.y) and abs(n.x)>abs(n.z): uv.append((p.y,p.z))
                            elif abs(n.z)>abs(n.y): uv.append((p.x,p.y))
                            else: uv.append((p.x,p.z))
                        face.append(lookup[k])
                    indices.extend([face[0],face[2],face[1]])
                ev.to_mesh_clear()
            string(f,part); string(f,role); f.write(struct.pack('<ii',len(vertices),len(indices)))
            for p,n,t in zip(vertices,normals,uv): f.write(struct.pack('<8f',*p,*n,*t))
            f.write(struct.pack('<'+'i'*len(indices),*indices)); totalv+=len(vertices); totalt+=len(indices)//3
    return dict(file=path.name,vertices=totalv,triangles=totalt,draws=len(buckets),bytes=path.stat().st_size)

def studio():
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=40
    scene.cycles.use_denoising=True
    scene.world.color=(.24,.24,.24)
    scene.render.resolution_x=1400; scene.render.resolution_y=900; scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX'
    bpy.ops.mesh.primitive_plane_add(size=200); floor=bpy.context.object; floor.name='Studio floor'
    floor.data.materials.append(material('Studio floor',(.075,.085,.10),.1,.42))
    floor.location.z=-.015
    for name,loc,power,size in [('Key',(1,3,7),2100,6),('Rim',(-4,-2,4),1700,5),('Front',(4,4,3),1100,4)]:
        data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='RECTANGLE'; data.size=size; data.size_y=size*.55
        o=bpy.data.objects.new(name,data); scene.collection.objects.link(o); o.location=loc; o.rotation_euler=(Vector((0,0,.6))-o.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('Review camera'); cam=bpy.data.objects.new('Review camera',data); scene.collection.objects.link(cam); scene.camera=cam
    cam.location=(6.2,7.7,3.6); cam.rotation_euler=(Vector((0,0,.65))-cam.location).to_track_quat('-Z','Y').to_euler(); data.lens=56
    return cam

stats=[]; collections={}
for kind in ['Player','Sedan','Hatch','Van','Pickup','Bus','Tanker']:
    coll=None
    if not PORTABLE:
        coll=bpy.data.collections.new(kind); bpy.context.scene.collection.children.link(coll)
    start=len(objects); info=passenger(kind,DESIGNS[kind]) if kind in DESIGNS else heavy(kind)
    preview_wheels(info,kind); car=objects[start:]
    if not PORTABLE:
        for o in car:
            for c in list(o.users_collection): c.objects.unlink(o)
            coll.objects.link(o)
    stats.append(export(kind,info,car)); collections[kind]=car if PORTABLE else coll
    print('EXPORTED',stats[-1],flush=True)
(ART/'mesh-budget.json').write_text(json.dumps(stats,indent=2))
if PORTABLE:
    if '--no-render' not in sys.argv:
        portable.render(collections['Player'], ART/'Player-studio.png', (6.2,7.7,3.6), (0,0,.65))
        portable.render(collections['Player'], ART/'Player-rear.png', (-5.8,-7.4,3.5), (0,0,.65))
        portable.contact_sheet(collections, ART/'Fleet.png')
else:
    cam=studio()
    for name,coll in collections.items(): coll.hide_render=name!='Player'; coll.hide_viewport=name!='Player'
    bpy.ops.wm.save_as_mainfile(filepath=str(ART/'CrashoutVehicles.blend'))
    if '--no-render' not in sys.argv:
        bpy.context.scene.render.filepath=str(ART/'Player-studio.png'); bpy.ops.render.render(write_still=True)
print('VEHICLE BUILD COMPLETE',flush=True)
