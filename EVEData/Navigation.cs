﻿using EVEDataUtils;

namespace SMT.EVEData
{
    public enum RoutingMode
    {
        Shortest,
        Safest,
        PreferLow,
    }

    public class Navigation
    {
        public enum GateType
        {
            StarGate,
            Ansiblex,
            JumpTo,
            Thera,
            Zarzakh,
        }

        private static Dictionary<string, MapNode> MapNodes { get; set; }
        private static List<string> TheraLinks { get; set; }
        private static List<string> ZarzakhLinks { get; set; }

        public static void ClearJumpBridges()
        {
            foreach (MapNode mn in MapNodes.Values)
            {
                mn.JBConnection = null;
            }
        }

        public static void ClearTheraConnections()
        {
            foreach (MapNode mn in MapNodes.Values)
            {
                mn.TheraConnections = null;
            }
        }

        public static void ClearZarzakhConnections()
        {
            foreach (MapNode mn in MapNodes.Values)
            {
                mn.ZarzakhConnection = null;
            }
        }

        public static void UpdateTheraConnections(List<string> theraSystems)
        {
            ClearTheraConnections();

            foreach (string ts in theraSystems)
            {
                MapNodes[ts].TheraConnections = theraSystems;
            }
        }

        public static void UpdateZarzakhConnections(List<string> zazahkSystems)
        {
            ClearZarzakhConnections();

            foreach (string ts in zazahkSystems)
            {
                MapNodes[ts].ZarzakhConnection = zazahkSystems;
            }
        }

        public static List<string> GetSystemsWithinXLYFrom(string start, double LY, bool includeHighSecSystems, bool includePochvenSystems)
        {
            List<string> inRange = new List<string>();

            MapNode startSys = null;

            foreach (MapNode sys in MapNodes.Values)
            {
                if (sys.Name == start)
                {
                    startSys = sys;
                    break;
                }
            }

            foreach (MapNode sys in MapNodes.Values)
            {
                if (sys == startSys)
                {
                    continue;
                }

                decimal x = startSys.X - sys.X;
                decimal y = startSys.Y - sys.Y;
                decimal z = startSys.Z - sys.Z;

                decimal length = DecimalMath.DecimalEx.Sqrt((x * x) + (y * y) + (z * z));

                bool shouldAdd = false;

                if (length < (decimal)LY)
                {
                    shouldAdd = true;
                }

                if (sys.HighSec & !includeHighSecSystems)
                {
                    shouldAdd = false;
                }

                if (sys.Pochven & !includePochvenSystems)
                {
                    shouldAdd = false;
                }

                if (shouldAdd)
                {
                    inRange.Add(sys.Name);
                }
            }

            return inRange;
        }

        public static List<string> GetSystemsXJumpsFrom(List<string> sysList, string start, int X)
        {
            if (MapNodes == null || !MapNodes.ContainsKey(start))
            {
                return sysList;
            }

            if (X != 0)
            {
                if (!sysList.Contains(start))
                {
                    sysList.Add(start);
                }

                MapNode mn = MapNodes[start];

                foreach (string mm in mn.Connections)
                {
                    if (!sysList.Contains(mm))
                    {
                        sysList.Add(mm);
                    }

                    List<string> connected = GetSystemsXJumpsFrom(sysList, mm, X - 1);
                    foreach (string s in connected)
                    {
                        if (!sysList.Contains(s))
                        {
                            sysList.Add(s);
                        }
                    }
                }
            }
            return sysList;
        }

        public static SerializableDictionary<string, List<string>> CreateStaticNavigationCache(List<System> eveSystems)
        {
            SerializableDictionary<string, List<string>> rangeCache = new SerializableDictionary<string, List<string>>();

            decimal maxRange = 12;

            // now create the jumpable system links
            foreach (System sysa in eveSystems)
            {
                foreach (System sysb in eveSystems)
                {
                    if (sysa == sysb)
                    {
                        continue;
                    }
                    // cant jump into highsec systems
                    if (sysb.TrueSec > 0.45)
                    {
                        continue;
                    }

                    // cant jump into Pochven systems
                    if (sysb.Region == "波赫文")
                    {
                        continue;
                    }

                    // cant jump into Zarzakh
                    if (sysb.Name == "赞颂之域")
                    {
                        continue;
                    }

                    decimal Distance = EveManager.Instance.GetRangeBetweenSystems(sysa.Name, sysb.Name);
                    if (Distance < maxRange && Distance > 0)
                    {
                        if (!rangeCache.ContainsKey(sysa.Name))
                        {
                            rangeCache[sysa.Name] = new List<string>();
                        }

                        rangeCache[sysa.Name].Add(sysb.Name);
                    }
                }
            }

            return rangeCache;
        }

        public static void InitNavigation(List<System> eveSystems, List<JumpBridge> jumpBridges, SerializableDictionary<string, List<string>> jumpRangeCache)
        {
            MapNodes = new Dictionary<string, MapNode>();

            TheraLinks = new List<string>();
            ZarzakhLinks = new List<string>();

            // 构建导航结构
            foreach (System sys in eveSystems)
            {
                MapNode mn = new MapNode
                {
                    Name = sys.Name,
                    HighSec = sys.TrueSec > 0.45,
                    Pochven = sys.Region == "波赫文",
                    Connections = new List<string>(),
                    JumpableSystems = new List<JumpLink>(),
                    Cost = 1,
                    MinCostToStart = 0,
                    X = sys.ActualX,
                    Y = sys.ActualY,
                    Z = sys.ActualZ,
                    F = 0,
                    ActualSystem = sys
                };

                // 添加系统连接
                foreach (string s in sys.Jumps)
                {
                    mn.Connections.Add(s);
                }

                MapNodes[mn.Name] = mn;
            }

            // 更新跳跃桥
            UpdateJumpBridges(jumpBridges);

            decimal MaxRange = 12;

            // 构建跳跃范围缓存
            foreach (string s in jumpRangeCache.Keys)
            {
                MapNode sysMN = MapNodes[s];
                foreach (string t in jumpRangeCache[s])
                {
                    decimal Distance = EveManager.Instance.GetRangeBetweenSystems(sysMN.Name, t);
                    if (Distance < MaxRange && Distance > 0)
                    {
                        JumpLink jl = new JumpLink
                        {
                            System = t,
                            RangeLY = Distance
                        };
                        sysMN.JumpableSystems.Add(jl);
                    }
                }
            }
        }


        public static List<RoutePoint> Navigate(string From, string To, bool UseJumpGates, bool UseThera, bool UseZarzakh, RoutingMode routingMode)
        {
            if (!(MapNodes.ContainsKey(From)) || !(MapNodes.ContainsKey(To)) || From == "" || To == "")

            {
                return null;
            }

            // clear the scores, values and parents from the list
            foreach (MapNode mapNode in MapNodes.Values)
            {
                mapNode.NearestToStart = null;
                mapNode.MinCostToStart = 0;
                mapNode.Visited = false;

                switch (routingMode)
                {
                    case RoutingMode.PreferLow:
                        {
                            if (mapNode.HighSec)
                                mapNode.Cost = 1000;
                        }
                        break;

                    case RoutingMode.Safest:
                        {
                            if (!mapNode.HighSec)
                                mapNode.Cost = 1000;
                        }
                        break;

                    case RoutingMode.Shortest:
                        mapNode.Cost = 1;
                        break;
                }
            }

            MapNode Start = MapNodes[From];
            MapNode End = MapNodes[To];

            List<MapNode> OpenList = new List<MapNode>();
            List<MapNode> ClosedList = new List<MapNode>();

            MapNode CurrentNode = null;

            // add the start to the open list
            OpenList.Add(Start);

            while (OpenList.Count > 0)
            {
                // get the MapNode with the lowest F score
                double lowest = OpenList.Min(mn => mn.MinCostToStart);
                CurrentNode = OpenList.First(mn => mn.MinCostToStart == lowest);

                // add the list to the closed list
                ClosedList.Add(CurrentNode);

                // remove it from the open list
                OpenList.Remove(CurrentNode);

                // walk the connections
                foreach (string connectionName in CurrentNode.Connections)
                {
                    MapNode CMN = MapNodes[connectionName];

                    if (CMN.Visited)
                        continue;

                    if (CMN.MinCostToStart == 0 || CurrentNode.MinCostToStart + CMN.Cost < CMN.MinCostToStart)
                    {
                        CMN.MinCostToStart = CurrentNode.MinCostToStart + CMN.Cost;
                        CMN.NearestToStart = CurrentNode;
                        if (!OpenList.Contains(CMN))
                        {
                            OpenList.Add(CMN);
                        }
                    }
                }

                if (UseJumpGates && CurrentNode.JBConnection != null)
                {
                    MapNode JMN = MapNodes[CurrentNode.JBConnection];
                    if (!JMN.Visited && JMN.MinCostToStart == 0 || CurrentNode.MinCostToStart + JMN.Cost < JMN.MinCostToStart)
                    {
                        JMN.MinCostToStart = CurrentNode.MinCostToStart + JMN.Cost;
                        JMN.NearestToStart = CurrentNode;
                        if (!OpenList.Contains(JMN))
                        {
                            OpenList.Add(JMN);
                        }
                    }
                }

                if (UseThera && CurrentNode.TheraConnections != null)
                {
                    foreach (string theraConnection in CurrentNode.TheraConnections)
                    {
                        MapNode CMN = MapNodes[theraConnection];

                        if (CMN.Visited)
                            continue;

                        // dont jump back to the system we came from 
                        if (CurrentNode.Name == theraConnection)
                            continue;

                        if (CMN.MinCostToStart == 0 || CurrentNode.MinCostToStart + CMN.Cost < CMN.MinCostToStart)
                        {
                            CMN.MinCostToStart = CurrentNode.MinCostToStart + CMN.Cost;
                            CMN.NearestToStart = CurrentNode;
                            if (!OpenList.Contains(CMN))
                            {
                                OpenList.Add(CMN);
                            }
                        }
                    }
                }

                if (UseZarzakh && CurrentNode.ZarzakhConnection != null)
                {
                    foreach (string ZarzakhConnection in CurrentNode.ZarzakhConnection)
                    {
                        MapNode CMN = MapNodes[ZarzakhConnection];

                        if (CMN.Visited)
                            continue;

                        // don't jump back to the system we just came from
                        if (CurrentNode.Name == ZarzakhConnection)
                            continue;

                        if (CMN.MinCostToStart == 0 || CurrentNode.MinCostToStart + CMN.Cost < CMN.MinCostToStart)
                        {
                            CMN.MinCostToStart = CurrentNode.MinCostToStart + CMN.Cost;
                            CMN.NearestToStart = CurrentNode;
                            if (!OpenList.Contains(CMN))
                            {
                                OpenList.Add(CMN);
                            }
                        }
                    }
                }

                /* Todo :  Additional error checking
                if (UseThera && !string.IsNullOrEmptyCurrent(Node.TheraInSig))
                {
                    //SJS HERE ERROR
                }
                */

                CurrentNode.Visited = true;
            }

            // build the path

            List<string> Route = new List<string>();

            bool rootError = false;

            CurrentNode = End;
            if (End.NearestToStart != null)
            {
                while (CurrentNode != null)
                {
                    Route.Add(CurrentNode.Name);
                    CurrentNode = CurrentNode.NearestToStart;
                    if (Route.Count > 2000)
                    {
                        rootError = true;
                        break;
                    }
                }
                Route.Reverse();
            }

            List<RoutePoint> ActualRoute = new List<RoutePoint>();

            if (!rootError)
            {
                for (int i = 0; i < Route.Count; i++)
                {
                    RoutePoint RP = new RoutePoint();
                    RP.SystemName = Route[i];
                    RP.ActualSystem = EveManager.Instance.GetEveSystem(Route[i]);
                    RP.GateToTake = GateType.StarGate;
                    RP.LY = 0.0m;

                    if (i < Route.Count - 1)
                    {
                        MapNode mn = MapNodes[RP.SystemName];
                        if (mn.JBConnection != null && mn.JBConnection == Route[i + 1])
                        {
                            RP.GateToTake = GateType.Ansiblex;
                        }

                        if (UseThera && mn.TheraConnections != null && mn.TheraConnections.Contains(Route[i + 1]))
                        {
                            RP.GateToTake = GateType.Thera;
                        }

                        if(UseZarzakh && mn.ZarzakhConnection != null && mn.ZarzakhConnection.Contains(Route[i + 1]))
                        {
                            RP.GateToTake = GateType.Zarzakh;
                        }
                    }
                    ActualRoute.Add(RP);
                }
            }

            return ActualRoute;
        }

        public static List<RoutePoint> NavigateCapitals(string from, string to, double maxLY, LocalCharacter lc, List<string> systemsToAvoid)
        {
            // 输入检查
            if (!MapNodes.ContainsKey(from) || !MapNodes.ContainsKey(to) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                return null;
            }

            double extraJumpFactor = 5.0;
            double avoidFactor = 0.0;

            // 清除路径信息
            foreach (MapNode mapNode in MapNodes.Values)
            {
                mapNode.NearestToStart = null;
                mapNode.MinCostToStart = 0;
                mapNode.Visited = false;
            }

            MapNode startNode = MapNodes[from];
            MapNode endNode = MapNodes[to];

            List<MapNode> openList = new List<MapNode> { startNode };
            List<MapNode> closedList = new List<MapNode>();

            MapNode currentNode = null;

            // A* 路径搜索算法
            while (openList.Count > 0)
            {
                currentNode = openList.OrderBy(n => n.MinCostToStart).First();
                closedList.Add(currentNode);
                openList.Remove(currentNode);

                // 处理当前节点的所有连接
                foreach (JumpLink connection in currentNode.JumpableSystems)
                {
                    if (connection.RangeLY > (decimal)maxLY)
                        continue;

                    MapNode connectedNode = MapNodes[connection.System];

                    if (connectedNode.Visited)
                        continue;

                    avoidFactor = systemsToAvoid.Contains(connection.System) ? 10000 : 0.0;

                    double costToConnectedNode = currentNode.MinCostToStart + (double)connection.RangeLY + extraJumpFactor + avoidFactor;

                    if (connectedNode.MinCostToStart == 0 || costToConnectedNode < connectedNode.MinCostToStart)
                    {
                        connectedNode.MinCostToStart = costToConnectedNode;
                        connectedNode.NearestToStart = currentNode;
                        if (!openList.Contains(connectedNode))
                        {
                            openList.Add(connectedNode);
                        }
                    }
                }

                currentNode.Visited = true;
            }

            // 构建路径
            List<string> route = new List<string>();
            currentNode = endNode;

            if (endNode.NearestToStart != null)
            {
                while (currentNode != null)
                {
                    route.Add(currentNode.Name);
                    currentNode = currentNode.NearestToStart;
                }
            }

            if (route.Count == 0)
            {
                return new List<RoutePoint>();
            }

            List<RoutePoint> actualRoute = new List<RoutePoint>();

            for (int i = 0; i < route.Count; i++)
            {
                RoutePoint routePoint = new RoutePoint
                {
                    GateToTake = GateType.JumpTo,
                    LY = i > 0 ? EveManager.Instance.GetRangeBetweenSystems(route[i], route[i - 1]) : 0.0m,
                    SystemName = route[i]
                };
                actualRoute.Add(routePoint);
            }

            actualRoute.Reverse();
            return actualRoute;
        }


        public static void UpdateJumpBridges(List<JumpBridge> jumpBridges)
        {
            foreach (JumpBridge jb in jumpBridges)
            {
                if (jb.Disabled)
                {
                    continue;
                }

                MapNodes[jb.From].JBConnection = jb.To;
                MapNodes[jb.To].JBConnection = jb.From;
            }
        }

        public static void UpdateTheraInfo(List<TheraConnection> theraList)
        {
            TheraLinks.Clear();
            foreach (MapNode mapNode in MapNodes.Values)
            {
                mapNode.TheraInSig = string.Empty;
                mapNode.TheraOutSig = string.Empty;
            }

            foreach (TheraConnection tc in theraList)
            {
                MapNode mn = MapNodes[tc.System];
                mn.TheraInSig = tc.InSignatureID;
                mn.TheraOutSig = tc.OutSignatureID;

                TheraLinks.Add(tc.System);
            }
        }

        public static void UpdateZarzakhInfo(List<string> zarzakhList)
        {
            ZarzakhLinks.Clear();

            foreach (string zc in zarzakhList)
            {
                ZarzakhLinks.Add(zc);
            }
        }


        private struct JumpLink
        {
            public decimal RangeLY;
            public string System;
        }

        public class RoutePoint
        {
            public GateType GateToTake { get; set; }
            public decimal LY { get; set; }
            public string SystemName { get; set; }

            public System ActualSystem { get; set; }

            public override string ToString()
            {
                string s = SystemName;
                if (GateToTake == GateType.Ansiblex)
                {
                    s += " (安塞波)";
                }

                if (GateToTake == GateType.Thera)
                {
                    s += " (希拉)";
                }

                if (GateToTake == GateType.Zarzakh)
                {
                    s += " (赞颂之域)";
                }

                if (GateToTake == GateType.JumpTo && LY > 0.0m)
                {
                    s += " (Jump To, Range " + LY.ToString("0.##") + " )";
                }

                return s;
            }
        }

        private class MapNode
        {
            public double Cost;
            public double F;
            public string JBConnection;
            public List<string> TheraConnections;
            public List<string> ZarzakhConnection;
            public double MinCostToStart;
            public MapNode NearestToStart;
            public string TheraInSig;
            public string TheraOutSig;
            public bool Visited;
            public decimal X;
            public decimal Y;
            public decimal Z;
            public List<string> Connections { get; set; }
            public bool HighSec { get; set; }
            public bool Pochven { get; set; }
            public List<JumpLink> JumpableSystems { get; set; }
            public string Name { get; set; }
            public System ActualSystem { get; set; }
        }
    }
}