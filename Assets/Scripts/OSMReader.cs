using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

public class OSMReader : MonoBehaviour
{
    const int MaxBuildings = 2000;

    [Header("Materials")]
    public Material roadMaterial;
    public Material waterMaterial;
    public Material wallMaterial;
    public Material roofMaterial;

    void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "asakusa.osm");
        if (!File.Exists(path))
        {
            Debug.LogError("OSMReader: asakusa.osm not found at " + path);
            return;
        }

        EnsureMaterials();

        XmlDocument doc = new XmlDocument();
        doc.Load(path);

        XmlNode boundsNode = doc.SelectSingleNode("//bounds");
        double minLat = double.Parse(boundsNode.Attributes["minlat"].Value);
        double maxLat = double.Parse(boundsNode.Attributes["maxlat"].Value);
        double minLon = double.Parse(boundsNode.Attributes["minlon"].Value);
        double maxLon = double.Parse(boundsNode.Attributes["maxlon"].Value);
        double centerLat = (minLat + maxLat) / 2.0;
        double centerLon = (minLon + maxLon) / 2.0;

        Dictionary<string, Vector2d> nodes = new Dictionary<string, Vector2d>();
        XmlNodeList nodeList = doc.SelectNodes("//node");
        foreach (XmlNode n in nodeList)
        {
            string id = n.Attributes["id"].Value;
            double lat = double.Parse(n.Attributes["lat"].Value);
            double lon = double.Parse(n.Attributes["lon"].Value);
            nodes[id] = new Vector2d(lat, lon);
        }

        XmlNodeList wayList = doc.SelectNodes("//way");
        int highwayCount = 0;
        int waterwayCount = 0;
        int buildingCount = 0;

        foreach (XmlNode way in wayList)
        {
            WayType type = ClassifyWay(way);
            if (type == WayType.None)
                continue;

            if (type == WayType.Building && buildingCount >= MaxBuildings)
                continue;

            List<Vector3> points = new List<Vector3>();
            XmlNodeList ndRefs = way.SelectNodes("nd");
            foreach (XmlNode nd in ndRefs)
            {
                string refId = nd.Attributes["ref"].Value;
                if (nodes.TryGetValue(refId, out Vector2d coord))
                {
                    Vector3 pos = LatLonToUnity(coord.x, coord.y, centerLat, centerLon);
                    points.Add(pos);
                }
            }

            if (points.Count < 2)
                continue;

            switch (type)
            {
                case WayType.Highway:
                    CreateStrip("Road_" + highwayCount, points, 0.5f, 0.05f, roadMaterial);
                    highwayCount++;
                    break;
                case WayType.Waterway:
                    CreateStrip("Water_" + waterwayCount, points, 2.0f, 0.02f, waterMaterial);
                    waterwayCount++;
                    break;
                case WayType.Building:
                    if (points.Count >= 3)
                    {
                        CreateBuilding("Bldg_" + buildingCount, points);
                        buildingCount++;
                    }
                    break;
            }
        }

        Debug.Log($"OSMReader: Loaded {highwayCount} roads, {waterwayCount} waterways, {buildingCount} buildings");
    }

    void EnsureMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");

        if (roadMaterial == null)
        {
            roadMaterial = new Material(litShader);
            roadMaterial.SetColor("_BaseColor", new Color(0.353f, 0.353f, 0.353f)); // #A0A0A0
        }
        if (waterMaterial == null)
        {
            waterMaterial = new Material(litShader);
            waterMaterial.SetColor("_BaseColor", new Color(0.068f, 0.278f, 0.694f)); // #4A90D9
        }
        if (wallMaterial == null)
        {
            wallMaterial = new Material(litShader);
            wallMaterial.SetColor("_BaseColor", new Color(0.659f, 0.482f, 0.305f)); // #D4B896
        }
        if (roofMaterial == null)
        {
            roofMaterial = new Material(litShader);
            roofMaterial.SetColor("_BaseColor", new Color(0.258f, 0.057f, 0.005f)); // #8B4513
        }
    }

    enum WayType { None, Highway, Waterway, Building }

    WayType ClassifyWay(XmlNode way)
    {
        XmlNodeList tags = way.SelectNodes("tag");
        foreach (XmlNode tag in tags)
        {
            string k = tag.Attributes["k"].Value;
            if (k == "building")
                return WayType.Building;
            if (k == "highway")
                return WayType.Highway;
            if (k == "waterway")
                return WayType.Waterway;
        }
        return WayType.None;
    }

    Vector3 LatLonToUnity(double lat, double lon, double centerLat, double centerLon)
    {
        // Scale 1/10: 1 Unity unit = 10 real meters
        double metersPerDegreeLat = 11132.0;
        double metersPerDegreeLon = 11132.0 * System.Math.Cos(centerLat * System.Math.PI / 180.0);

        float x = (float)((lon - centerLon) * metersPerDegreeLon);
        float z = (float)((lat - centerLat) * metersPerDegreeLat);

        return new Vector3(x, 0f, z);
    }

    void CreateStrip(string objName, List<Vector3> points, float width, float yPos, Material mat)
    {
        int n = points.Count;
        float halfW = width * 0.5f;

        Vector3[] vertices = new Vector3[n * 2];
        int[] triangles = new int[(n - 1) * 6];

        for (int i = 0; i < n; i++)
        {
            Vector3 forward;
            if (i < n - 1)
                forward = (points[i + 1] - points[i]).normalized;
            else
                forward = (points[i] - points[i - 1]).normalized;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 p = new Vector3(points[i].x, yPos, points[i].z);
            vertices[i * 2 + 0] = p - right * halfW;
            vertices[i * 2 + 1] = p + right * halfW;
        }

        for (int i = 0; i < n - 1; i++)
        {
            int vi = i * 2;
            int ti = i * 6;
            triangles[ti + 0] = vi + 0;
            triangles[ti + 1] = vi + 2;
            triangles[ti + 2] = vi + 1;
            triangles[ti + 3] = vi + 1;
            triangles[ti + 4] = vi + 2;
            triangles[ti + 5] = vi + 3;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform);
        go.AddComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshRenderer>().material = mat;
    }

    void CreateBuilding(string objName, List<Vector3> footprint)
    {
        if (footprint.Count > 1 && Vector3.Distance(footprint[0], footprint[footprint.Count - 1]) < 0.001f)
            footprint.RemoveAt(footprint.Count - 1);

        if (footprint.Count < 3)
            return;

        if (CalculateSignedArea(footprint) > 0)
            footprint.Reverse();

        float height = Random.Range(0.3f, 1.5f);
        float roofOverhang = 0.03f;

        GameObject buildingRoot = new GameObject(objName);
        buildingRoot.transform.SetParent(transform);

        CreateWallMesh(buildingRoot, footprint, height);
        CreateRoofMesh(buildingRoot, footprint, height, roofOverhang);
    }

    void CreateWallMesh(GameObject parent, List<Vector3> footprint, float height)
    {
        int n = footprint.Count;
        Vector3[] vertices = new Vector3[n * 4];
        int[] triangles = new int[n * 6];

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            Vector3 bl = footprint[i];
            Vector3 br = footprint[next];
            Vector3 tl = bl + Vector3.up * height;
            Vector3 tr = br + Vector3.up * height;

            int vi = i * 4;
            vertices[vi + 0] = bl;
            vertices[vi + 1] = br;
            vertices[vi + 2] = tl;
            vertices[vi + 3] = tr;

            int ti = i * 6;
            triangles[ti + 0] = vi + 0;
            triangles[ti + 1] = vi + 2;
            triangles[ti + 2] = vi + 1;
            triangles[ti + 3] = vi + 1;
            triangles[ti + 4] = vi + 2;
            triangles[ti + 5] = vi + 3;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        GameObject wallObj = new GameObject("Wall");
        wallObj.transform.SetParent(parent.transform, false);
        wallObj.AddComponent<MeshFilter>().mesh = mesh;
        wallObj.AddComponent<MeshRenderer>().material = wallMaterial;
    }

    void CreateRoofMesh(GameObject parent, List<Vector3> footprint, float height, float overhang)
    {
        int n = footprint.Count;

        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < n; i++)
            centroid += footprint[i];
        centroid /= n;

        Vector3[] roofPoints = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 dir = (footprint[i] - centroid).normalized;
            roofPoints[i] = footprint[i] + dir * overhang + Vector3.up * height;
        }

        Vector3[] vertices = new Vector3[n + 1];
        Vector3 roofCenter = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            vertices[i] = roofPoints[i];
            roofCenter += roofPoints[i];
        }
        roofCenter /= n;
        vertices[n] = roofCenter;

        int[] triangles = new int[n * 3];
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            triangles[i * 3 + 0] = n;
            triangles[i * 3 + 1] = i;
            triangles[i * 3 + 2] = next;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        GameObject roofObj = new GameObject("Roof");
        roofObj.transform.SetParent(parent.transform, false);
        roofObj.AddComponent<MeshFilter>().mesh = mesh;
        roofObj.AddComponent<MeshRenderer>().material = roofMaterial;
    }

    float CalculateSignedArea(List<Vector3> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            int next = (i + 1) % polygon.Count;
            area += polygon[i].x * polygon[next].z;
            area -= polygon[next].x * polygon[i].z;
        }
        return area * 0.5f;
    }

    struct Vector2d
    {
        public double x;
        public double y;

        public Vector2d(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
    }
}
