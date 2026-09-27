using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace FlyPet;

public sealed class FlyRenderer : IDisposable
{
    readonly Bitmap low = new(144,144,PixelFormat.Format32bppPArgb);
    readonly List<Face> faces = new(1000);
    record struct Face(Vector3 A,Vector3 B,Vector3 C,Color Color,int Grain);
    record struct Projected(PointF[] Points,float Depth,Color Color);
    readonly List<Projected> projected=new(1000);
    static Color Body=Color.FromArgb(70,63,44),Thorax=Color.FromArgb(52,58,47),Eye=Color.FromArgb(135,42,26);
    public void Draw(Graphics output,Rectangle target,Simulation sim,Rectangle desktop,bool meters=true)
    {
        using var g=Graphics.FromImage(low);g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.None;
        if(sim.Dead)
        {
            DrawPuddle(g,sim);
            var deadState=output.Save();output.InterpolationMode=InterpolationMode.NearestNeighbor;output.PixelOffsetMode=PixelOffsetMode.Half;
            output.DrawImage(low,target,0,0,144,144,GraphicsUnit.Pixel);output.Restore(deadState);return;
        }
        float nx=(sim.Position.X-desktop.Left)/Math.Max(1,desktop.Width)*2-1;
        float ny=(sim.Position.Y-desktop.Top)/Math.Max(1,desktop.Height)*2-1;
        float bob=sim.Dead?0:MathF.Sin(sim.Time*6)*1.8f;
        float shadowSpread=sim.Dead?1.25f:1+.08f*MathF.Sin(sim.Time*6);
        using(var shadow=new SolidBrush(Color.FromArgb(24,7,6,4)))g.FillEllipse(shadow,38+nx*12,87+ny*7,68*shadowSpread,17*shadowSpread);
        using(var coreShadow=new SolidBrush(Color.FromArgb(48,7,6,4)))g.FillEllipse(coreShadow,50+nx*10,90+ny*6,43*shadowSpread,9*shadowSpread);
        faces.Clear();projected.Clear();
        // Overlapping tapered abdominal segments create a readable volume and self-occlusion.
        Ellipsoid(new(0,.31f,-.02f),new(.39f,.50f,.38f),Color.FromArgb(73,67,47),1);
        Ellipsoid(new(0,.65f,-.035f),new(.35f,.43f,.34f),Color.FromArgb(59,53,37),3);
        Ellipsoid(new(0,.96f,-.06f),new(.28f,.34f,.28f),Color.FromArgb(82,69,42),5);
        Ellipsoid(new(0,1.19f,-.08f),new(.19f,.23f,.20f),Color.FromArgb(48,43,31),7);
        // Low resolution facets and asymmetric grain are generated, not copied textures.
        Ellipsoid(new(0,-.18f,.08f),new(.48f,.50f,.43f),Thorax,17);
        Ellipsoid(new(0,-.72f,.11f),new(.42f,.34f,.35f),Color.FromArgb(89,78,52),29);
        Ellipsoid(new(-.32f,-.76f,.22f),new(.23f,.28f,.26f),Eye,37);
        Ellipsoid(new(.32f,-.76f,.22f),new(.23f,.28f,.26f),Eye,47);
        float flap=sim.Grounded?.06f:MathF.Sin(sim.Time*135)*.52f+.35f;
        for(int side=-1;side<=1;side+=2)
        {
            Vector3 a=new(side*.19f,-.31f,.25f),b=new(side*1.75f,-.02f,flap),c=new(side*1.46f,.72f,flap*.82f),d=new(side*.54f,.86f,.25f);
            faces.Add(new(a,b,c,Color.FromArgb(126,161,170,151),51));faces.Add(new(a,c,d,Color.FromArgb(154,111,130,122),52));
        }
        var rot=Matrix4x4.CreateRotationY(sim.Dead?MathF.PI:0)*Matrix4x4.CreateRotationZ(sim.Heading)*Matrix4x4.CreateRotationY(nx*.65f)*Matrix4x4.CreateRotationX(-ny*.50f-.27f);
        PointF Project(Vector3 v){v=Vector3.Transform(v,rot);float p=4.1f/(4.1f-v.Z);return new(72+v.X*28*p,64+v.Y*28*p-v.Z*11-bob);}
        using(var legPen=new Pen(Color.FromArgb(47,42,28),2.1f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
        using(var lightPen=new Pen(Color.FromArgb(116,102,62),.8f))
        {
            for(int side=-1;side<=1;side+=2)for(int j=0;j<3;j++)
            {
                float y=-.42f+j*.40f;float gait=sim.Dead?-.12f:MathF.Sin(sim.Time*13+j*2.1f+side)*.13f;
                var a=Project(new(side*.31f,y,.00f));var b=Project(new(side*(.78f+gait),y+(j-1)*.27f,-.25f));var c=Project(new(side*(1.15f+gait),y+(j-1)*.57f,-.48f));
                g.DrawLines(legPen,[a,b,c]);g.DrawLine(lightPen,a,b);
                using var joint=new SolidBrush(Color.FromArgb(82,73,43));g.FillEllipse(joint,b.X-2,b.Y-2,4,4);
            }
        }
        foreach(var f in faces)
        {
            var a=Vector3.Transform(f.A,rot);var b=Vector3.Transform(f.B,rot);var c=Vector3.Transform(f.C,rot);
            var normal=Vector3.Cross(b-a,c-a);if(normal.LengthSquared()<1e-8)continue;normal=Vector3.Normalize(normal);
            if(f.Color.A<255&&normal.Z<0)normal=-normal;
            float diffuse=Math.Max(0,Vector3.Dot(normal,Vector3.Normalize(new(-.7f,-.9f,1.7f))));
            float rim=MathF.Pow(1-Math.Abs(normal.Z),2)*.24f;float light=.28f+.78f*diffuse+rim;
            // Two-sided surfaces for translucent wings; occluded body faces are sorted by depth.
            if(f.Color.A==255&&normal.Z<-.15f)continue;
            int grain=((f.Grain*71+17)%23)-11;
            int Ch(int v)=>Math.Clamp((int)(v*light)+grain,0,255);
            var col=sim.HitFlash>0?Color.FromArgb(f.Color.A,190,55,39):Color.FromArgb(f.Color.A,Ch(f.Color.R),Ch(f.Color.G),Ch(f.Color.B));
            projected.Add(new([Project(f.A),Project(f.B),Project(f.C)],(a.Z+b.Z+c.Z)/3,col));
        }
        projected.Sort((a,b)=>a.Depth.CompareTo(b.Depth));
        foreach(var p in projected)
        {
            using var brush=new SolidBrush(p.Color);g.FillPolygon(brush,p.Points);
            using var edge=new Pen(Color.FromArgb(p.Color.A<255?38:58,12,14,11),.55f);g.DrawPolygon(edge,p.Points);
        }
        using(var veins=new Pen(Color.FromArgb(135,51,67,57),.8f))
        for(int side=-1;side<=1;side+=2)
        {
            var a=Project(new(side*.19f,-.31f,.27f));var b=Project(new(side*1.72f,-.02f,flap+.02f));var c=Project(new(side*1.44f,.71f,flap*.82f+.02f));
            g.DrawLine(veins,a,b);g.DrawLine(veins,a,c);g.DrawLine(veins,Project(new(side*.75f,.22f,flap*.6f)),c);
        }
        using(var hair=new Pen(Color.FromArgb(47,40,28),1))
        {
            for(int s=-1;s<=1;s+=2){var root=Project(new(s*.15f,-.91f,.20f));var tip=Project(new(s*.29f,-1.27f,.28f));g.DrawLine(hair,root,tip);g.FillEllipse(Brushes.SaddleBrown,tip.X-1.5f,tip.Y-1.5f,3,3);}
            if(sim.Behavior=="吃糖")g.DrawLine(hair,Project(new(0,-.94f,.03f)),Project(new(0,-1.33f,-.29f)));
        }
        if(meters)
        {
            using var bg=new SolidBrush(Color.FromArgb(170,18,21,18));g.FillRectangle(bg,43,124,58,11);
            using var hp=new SolidBrush(sim.Dead?Color.FromArgb(154,70,52):Color.FromArgb(178,197,126));g.FillRectangle(hp,46,127,(int)(52*sim.Health/100),2);
            using var hunger=new SolidBrush(Color.FromArgb(198,152,90));g.FillRectangle(hunger,46,132,(int)(52*sim.Fullness/100),1);
        }
        var state=output.Save();output.InterpolationMode=InterpolationMode.NearestNeighbor;output.PixelOffsetMode=PixelOffsetMode.Half;
        output.DrawImage(low,target,0,0,144,144,GraphicsUnit.Pixel);output.Restore(state);
    }
    void Ellipsoid(Vector3 center,Vector3 radius,Color color,int seed)
    {
        const int rings=6,segs=10;
        Vector3 V(int r,int s){float p=MathF.PI*r/rings,a=MathF.Tau*s/segs;return center+new Vector3(MathF.Sin(p)*MathF.Cos(a)*radius.X,MathF.Cos(p)*radius.Y,MathF.Sin(p)*MathF.Sin(a)*radius.Z);}
        for(int r=0;r<rings;r++)for(int s=0;s<segs;s++)
        {
            var a=V(r,s);var b=V(r+1,s);var c=V(r+1,s+1);var d=V(r,s+1);
            Color col=color;if(seed==0&&r%2==0)col=Color.FromArgb(color.R*2/3,color.G*2/3,color.B*2/3);
            faces.Add(new(a,c,b,col,seed+r*segs+s));faces.Add(new(a,d,c,col,seed+r*segs+s+3));
        }
    }
    public static void DrawSugar(Graphics g,int size,float amount=100)
    {
        float s=size/48f;g.ScaleTransform(s,s);
        using var shadow=new SolidBrush(Color.FromArgb(45,10,8,4));g.FillEllipse(shadow,8,29,32,10);
        using var front=new SolidBrush(Color.FromArgb(194,184,140));g.FillPolygon(front,new Point[]{new(12,18),new(26,22),new(26,36),new(12,30)});
        using var side=new SolidBrush(Color.FromArgb(124,123,94));g.FillPolygon(side,new Point[]{new(26,22),new(36,15),new(36,29),new(26,36)});
        using var top=new SolidBrush(Color.FromArgb(237,225,181));g.FillPolygon(top,new Point[]{new(12,18),new(22,12),new(36,15),new(26,22)});
        using var grain=new SolidBrush(Color.FromArgb(183,175,129));for(int i=0;i<8;i++)g.FillRectangle(grain,15+i*7%15,19+i*5%11,2,2);
        using var bar=new SolidBrush(Color.FromArgb(189,203,142));g.FillRectangle(bar,11,41,26*amount/100,2);g.ResetTransform();
    }
    static void DrawPuddle(Graphics g,Simulation sim)
    {
        float pulse=MathF.Sin(sim.Time*2)*.8f;
        using var shadow=new SolidBrush(Color.FromArgb(43,8,6,4));g.FillEllipse(shadow,22,94,104,30);
        using var rim=new SolidBrush(Color.FromArgb(133,35,34,22));g.FillPolygon(rim,new Point[]{new(21,100),new(31,91),new(44,89),new(53,84),new(65,88),new(78,83),new(92,88),new(111,87),new(123,99),new(117,110),new(101,112),new(94,119),new(72,116),new(63,120),new(46,112),new(34,112)});
        using var bruise=new SolidBrush(Color.FromArgb(207,72,25,22));g.FillPolygon(bruise,new Point[]{new(28,100),new(43,93),new(56,94),new(62,89),new(75,92),new(93,89),new(115,98),new(107,108),new(91,110),new(82,116),new(66,111),new(49,112),new(34,107)});
        using var dark=new SolidBrush(Color.FromArgb(187,46,15,16));g.FillPolygon(dark,new Point[]{new(48,96),new(59,90),new(72,95),new(80,91),new(100,95),new(99,105),new(81,109),new(60,106),new(48,103)});
        using var goo=new SolidBrush(Color.FromArgb(181,136,91,43));
        g.FillPolygon(goo,new Point[]{new(62,93),new(75,88),new(84,93),new(83,103),new(67,105),new(59,101)});
        using var eye=new SolidBrush(Color.FromArgb(179,96,36,23));g.FillEllipse(eye,46,91,16,12);g.FillEllipse(eye,90,94,13,9);
        using var eyeGlint=new SolidBrush(Color.FromArgb(196,197,76,43));g.FillRectangle(eyeGlint,48,93,5,2);g.FillRectangle(eyeGlint,93,95,3,2);
        using var wing=new SolidBrush(Color.FromArgb(141,127,137,119));g.FillPolygon(wing,new Point[]{new(51,92),new(17,76),new(24,91),new(43,102)});g.FillPolygon(wing,new Point[]{new(94,94),new(128,79),new(124,98),new(105,104)});
        using var spatter=new SolidBrush(Color.FromArgb(145,115,31,24));
        foreach(var (x,y,r) in new (int,int,int)[]{(24,112,4),(14,100,3),(37,77,2),(110,75,3),(128,112,5),(100,124,3),(68,126,2),(40,120,2)})g.FillEllipse(spatter,x,y,r+Math.Abs(pulse)*.2f,r);
        using var wet=new Pen(Color.FromArgb(122,201,111,71),1);g.DrawLines(wet,new Point[]{new(54,94),new(62,91),new(70,92)});g.DrawLine(wet,84,92,94,93);
    }
    public void Dispose()=>low.Dispose();
}
