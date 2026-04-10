using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

public class OSMReader : MonoBehaviour
{
    void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "asakusa.osm");
        if (!File.Exists(path))
        {
            Debug.LogError("OSMReader: asakusa.osm not found at " + path);
            return;
        }

        XmlDocument doc = new XmlDocument();
        doc.Load(path);

        // Parse bounds for coordinate centering
        XmlNode boundsNode = doc.SelectSingleNode("//bounds");
        double minLat = double.Parse(boundsNode.Attributes["minlat"].Value);
        double maxLat = double.Parse(boundsNode.Attributes["maxlat"].Value);
        double minLon = double.Parse(boundsNode.Attributes["minlon"].Value);
        double maxLon = double.Parse(boundsNode.Attributes["maxlon"].Value);
        double centerLat = (minLat + maxLat) / 2.0;
        double centerLon = (minLon + maxLon) / 2.0;

        // Parse all nodes (lat/lon)
        Dictionary<string, Vector2d> nodes = new Dictionary<string, Vector2d>();
        XmlNodeList nodeList = doc.SelectNodes("//node");
        foreach (XmlNode n in nodeList)
        {
            string id = n.Attributes["id"].Value;
            double lat = double.Parse(n.Attributes["lat"].Value);
            double lon = double.Parse(n.Attributes["lon"].Value);
            nodes[id] = new Vector2d(lat, lon);
        }

        // Parse ways and classify by tag
        XmlNodeList wayList = doc.SelectNodes("//way");
        int highwayCount = 0;
        int waterwayCount = 0;
        int buildingCount = 0;

        foreach (XmlNode way in wayList)
        {
            WayType type = ClassifyWay(way);
            if (type == WayType.None)
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

            string objName;
            Color color;
            float width;

            switch (type)
            {
                case WayType.Highway:
                    objName = "Road_" + highwayCount;
                    color = Color.white;
                    width = 3.0f;
                    highwayCount++;
                    break;
                case WayType.Waterway:
                    objName = "Water_" + waterwayCount;
                    color = new Color(0.2f, 0.5f, 1.0f);
                    width = 8.0f;
                    waterwayCount++;
                    break;
                case WayType.Building:
                    objName = "Bldg_" + buildingCount;
                    color = new Color(0.6f, 0.6f, 0.6f);
                    width = 1.5f;
                    buildingCount++;
                    break;
                default:
                    continue;
            }

            CreateLine(objName, points, color, width);
        }

        Debug.Log($"OSMReader: Loaded {highwayCount} roads, {waterwayCount} waterways, {buildingCount} buildings");
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
        // 1 degree latitude ~ 111,320m, 1 degree longitude ~ 111,320m * cos(lat)
        double metersPerDegreeLat = 111320.0;
        double metersPerDegreeLon = 111320.0 * System.Math.Cos(centerLat * System.Math.PI / 180.0);

        float x = (float)((lon - centerLon) * metersPerDegreeLon);
        float z = (float)((lat - centerLat) * metersPerDegreeLat);

        return new Vector3(x, 0f, z);
    }

    void CreateLine(string objName, List<Vector3> points, Color color, float width)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
        lr.startWidth = width;
        lr.endWidth = width;
        lr.useWorldSpace = false;

        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
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
