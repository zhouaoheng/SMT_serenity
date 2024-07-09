namespace SMT.EVEData
{
    public class JumpRoute
    {
        public List<Navigation.RoutePoint> CurrentRoute { get; set; }
        public List<string> WayPoints { get; set; }
        public double MaxLY { get; set; }
        public int JDC { get; set; }

        public List<string> AvoidSystems { get; set; }

        public Dictionary<string, List<string>> AlternateMids { get; set; }

        public JumpRoute()
        {
            MaxLY = 7.0;
            JDC = 5;

            CurrentRoute = new List<Navigation.RoutePoint>();
            WayPoints = new List<string>();
            AvoidSystems = new List<string>();
            AlternateMids = new Dictionary<string, List<string>>();
        }

public void Recalculate()
{
    CurrentRoute.Clear();

    if (WayPoints.Count < 2)
    {
        return;
    }

    double actualMaxLY = JDC == 5 ? MaxLY : MaxLY * 0.9;

    string start = string.Empty;
    string end = WayPoints[0];

    List<string> avoidSystems = AvoidSystems;
    AlternateMids.Clear();

    for (int i = 1; i < WayPoints.Count; i++)
    {
        start = end;
        end = WayPoints[i];

        List<Navigation.RoutePoint> sysList = Navigation.NavigateCapitals(start, end, actualMaxLY, null, avoidSystems);

        if (sysList != null)
        {
            foreach (Navigation.RoutePoint s in sysList)
            {
                if (CurrentRoute.Count > 0 && CurrentRoute.Last().SystemName == s.SystemName)
                {
                    CurrentRoute.Last().LY = s.LY;
                }
                else
                {
                    CurrentRoute.Add(s);
                }
            }

            if (sysList.Count > 2)
            {
                for (int j = 2; j < sysList.Count; j++)
                {
                    List<string> systemsA = Navigation.GetSystemsWithinXLYFrom(CurrentRoute[j - 2].SystemName, MaxLY, false, false);
                    List<string> systemsB = Navigation.GetSystemsWithinXLYFrom(CurrentRoute[j].SystemName, MaxLY, false, false);

                    IEnumerable<string> alternatives = systemsA.Intersect(systemsB);

                    AlternateMids[CurrentRoute[j - 1].SystemName] = alternatives.Where(mid => mid != CurrentRoute[j - 1].SystemName).ToList();
                }
            }
        }
    }
}

    }
}