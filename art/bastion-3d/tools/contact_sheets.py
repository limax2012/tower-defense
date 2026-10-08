"""Assemble the current asset review renders into labeled contact sheets."""
from pathlib import Path
import json
import math
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[3]
ART=ROOT/'art/bastion-3d'
catalog=json.loads((ART/'catalog.json').read_text())
font_paths=[Path('C:/Windows/Fonts/bahnschrift.ttf'),Path('C:/Windows/Fonts/arial.ttf')]
font=next((ImageFont.truetype(str(p),21) for p in font_paths if p.exists()),ImageFont.load_default())

for category,columns in [('Towers',5),('Enemies',3),('Tactics',2),('Environment',4),('EnemyModules',4)]:
    items=[x for x in catalog['assets'] if x['category']==category and x['id']!='cinderworks_dressing']
    tile=320
    label=44
    rows=math.ceil(len(items)/columns)
    sheet=Image.new('RGB',(columns*tile,rows*(tile+label)),(12,22,34))
    draw=ImageDraw.Draw(sheet)
    for i,item in enumerate(items):
        path=ART/'review'/(item['id']+'-front.png')
        if not path.exists():
            continue
        image=Image.open(path).convert('RGB').resize((tile,tile),Image.Resampling.LANCZOS)
        x=(i%columns)*tile
        y=(i//columns)*(tile+label)
        sheet.paste(image,(x,y))
        name=item['displayName'].replace('foundry_','').replace('enemy_role_','').replace('_',' ').title()
        box=draw.textbbox((0,0),name,font=font)
        draw.text((x+(tile-(box[2]-box[0]))/2,y+tile+10),name,font=font,fill=(212,229,236))
    sheet.save(ART/'review'/(category.lower()+'-contact.png'))
