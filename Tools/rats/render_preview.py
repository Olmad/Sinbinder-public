#!/usr/bin/env python3
"""Paint an orthographic preview straight from shipped OBJ faces, not AI illustration."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np,trimesh,math
BASE=Path(__file__).resolve().parents[1]
MOD=BASE/'Assets/Resources/Rats/Models'
OUT=BASE/'PREVIEW_6_RATS.png'
COATS=[(87,82,74),(135,98,72),(110,111,103),(148,123,87),(85,102,85),(136,118,114)]
NAMES=[('Mother','Мама-крыса'),('Pip','Пискун'),('Dot','Пятнышко'),('Sprig','Прыг'),('Moss','Мох'),('Tuck','Соня')]
W,H=1680,1040
im=Image.new('RGB',(W,H),(27,30,34));d=ImageDraw.Draw(im)
fontpath='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
font=ImageFont.truetype(fontpath,32) if Path(fontpath).exists() else ImageFont.load_default()
small=ImageFont.truetype(fontpath,21) if Path(fontpath).exists() else ImageFont.load_default()
# camera from positive z and x, slightly above
camera=np.array([2.45,1.65,3.0]); camera=camera/np.linalg.norm(camera)
right=np.cross([0,1,0],camera);right=right/np.linalg.norm(right)
up=np.cross(camera,right);up=up/np.linalg.norm(up)
light=np.array([-0.1,1.0,1.0]);light=light/np.linalg.norm(light)

def palette(material,i):
    k=material.lower()
    if 'earinner' in k:return (178,107,111)
    if 'ear' in k:return tuple(int(c*.82) for c in COATS[i])
    if 'belly' in k:return tuple(int(c*.54+255*.46) for c in COATS[i])
    if 'eye' in k:return (17,17,19)
    if 'glint' in k:return (247,239,226)
    if 'nose' in k:return (198,112,114)
    if 'paw' in k:return (159,97,97)
    if 'whisk' in k:return (193,187,170)
    if 'tail' in k:return (169,102,104)
    return COATS[i]
for i,(short,label) in enumerate(NAMES):
    row=i//3;col=i%3
    x0=28+col*555; y0=32+row*510
    d.rounded_rectangle((x0,y0,x0+518,y0+465),radius=20,fill=(37,40,44),outline=(72,76,77),width=2)
    # body and tail are separate .obj in real Unity actor, reconstruct their relative attachment
    scale=0.52 if i==0 else [0,0.30,0.27,0.30,0.28,0.26][i]
    objects=[]
    for suffix in ['Body','Tail']:
        scene=trimesh.load(MOD/(short+suffix+'.obj'),force='scene',process=False)
        for name,mesh in scene.geometry.items():
            points=mesh.vertices.copy()
            if suffix=='Tail':points += np.array([0,.20,-.51])
            points*=scale
            objects.append((name,points,mesh.faces))
    face_draw=[]
    size=430 # projection scale in preview, not in game
    for name,verts,faces in objects:
        p=np.stack((verts@right,verts@up,verts@camera),axis=1)
        p[:,0]=x0+258 + p[:,0]*size
        p[:,1]=y0+280 - p[:,1]*size
        xyz=verts[faces]
        normals=np.cross(xyz[:,1]-xyz[:,0],xyz[:,2]-xyz[:,0]);
        norms=np.maximum(np.linalg.norm(normals,axis=1),1e-8)
        normals/=norms[:,None]
        shading=.60+.40*np.maximum(normals@light,0)
        rgb=palette(name,i)
        for k,face in enumerate(faces):
            coords=tuple((float(p[t,0]),float(p[t,1])) for t in face)
            face_draw.append((float(np.mean(p[face,2])),coords,tuple(int(max(0,min(255,c*shading[k]))) for c in rgb)))
    face_draw.sort(key=lambda x:x[0]) # far side before near side
    for _,coords,color in face_draw:d.polygon(coords,fill=color)
    d.text((x0+22,y0+20),label,font=font,fill=(229,222,201))
    d.text((x0+22,y0+416),'отдельный OBJ • анимируемый хвост',font=small,fill=(158,164,161))
# label
im.save(OUT)
print(OUT)
