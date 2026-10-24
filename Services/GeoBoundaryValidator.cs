using System.Text.Json;
namespace Nagorik.Api.Services;
public static class GeoBoundaryValidator
{
    private readonly record struct Point(double X,double Y);
    private static double Cross(Point a,Point b,Point c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
    private static bool On(Point a,Point b,Point p)=>Math.Abs(Cross(a,b,p))<1e-12&&p.X>=Math.Min(a.X,b.X)&&p.X<=Math.Max(a.X,b.X)&&p.Y>=Math.Min(a.Y,b.Y)&&p.Y<=Math.Max(a.Y,b.Y);
    private static bool Intersects(Point a,Point b,Point c,Point d)=>On(a,b,c)||On(a,b,d)||On(c,d,a)||On(c,d,b)||(Math.Sign(Cross(a,b,c))!=Math.Sign(Cross(a,b,d))&&Math.Sign(Cross(c,d,a))!=Math.Sign(Cross(c,d,b)));
    private static bool Contains(Point[] ring,Point p)
    {
        bool inside=false;for(int i=0,j=ring.Length-2;i<ring.Length-1;j=i++)
        {if(On(ring[j],ring[i],p))return false;if((ring[i].Y>p.Y)!=(ring[j].Y>p.Y)&&p.X<(ring[j].X-ring[i].X)*(p.Y-ring[i].Y)/(ring[j].Y-ring[i].Y)+ring[i].X)inside=!inside;}return inside;
    }
    public static string Normalize(string value)
    {
        try
        {
            if(value.Length>100000)throw new FormatException();using var document=JsonDocument.Parse(value);var root=document.RootElement;
            if(root.GetProperty("type").GetString()=="Feature")root=root.GetProperty("geometry");
            var type=root.GetProperty("type").GetString();var coordinates=root.GetProperty("coordinates");
            var polygons=type=="Polygon"?new[]{coordinates}:type=="MultiPolygon"?coordinates.EnumerateArray().ToArray():throw new FormatException();
            if(polygons.Length is 0 or >20)throw new FormatException();int total=0;
            foreach(var polygon in polygons)
            {
                var rings=new List<Point[]>();foreach(var ring in polygon.EnumerateArray())
                {
                    if(ring.GetArrayLength() is <4 or >500)throw new FormatException();total+=ring.GetArrayLength();if(total>2000)throw new FormatException();
                    var points=ring.EnumerateArray().Select(p=>
                    {if(p.GetArrayLength()!=2)throw new FormatException();var x=p[0].GetDouble();var y=p[1].GetDouble();if(!double.IsFinite(x)||!double.IsFinite(y)||!TransportSearchService.Inside(y,x))throw new FormatException();return new Point(x,y);}).ToArray();
                    if(points[0]!=points[^1]||points.Zip(points.Skip(1)).Any(p=>p.First==p.Second))throw new FormatException();
                    var area=points.Zip(points.Skip(1)).Sum(p=>p.First.X*p.Second.Y-p.Second.X*p.First.Y);if(Math.Abs(area)<1e-12)throw new FormatException();
                    for(var i=0;i<points.Length-1;i++)for(var j=i+1;j<points.Length-1;j++)if(j!=i+1&&!(i==0&&j==points.Length-2)&&Intersects(points[i],points[i+1],points[j],points[j+1]))throw new FormatException();
                    rings.Add(points);
                }
                if(rings.Count==0)throw new FormatException();
                for(var h=1;h<rings.Count;h++)
                {
                    if(!rings[h].Take(rings[h].Length-1).All(p=>Contains(rings[0],p)))throw new FormatException();
                    for(var previous=0;previous<h;previous++)
                    {
                        var a=rings[h];var b=rings[previous];for(var i=0;i<a.Length-1;i++)for(var j=0;j<b.Length-1;j++)if(Intersects(a[i],a[i+1],b[j],b[j+1]))throw new FormatException();
                        if(previous>0&&(Contains(b,a[0])||Contains(a,b[0])))throw new FormatException();
                    }
                }
            }
            return root.GetRawText();
        }
        catch(Exception e)when(e is JsonException or FormatException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException)
        {throw new TransportProblem(400,"Use a valid Dhaka GeoJSON Polygon/MultiPolygon geometry (or Feature): closed non-crossing rings, valid holes, at most 500 points per ring and 2,000 total.");}
    }
}
