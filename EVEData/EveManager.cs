//-----------------------------------------------------------------------
// EVE Manager
//-----------------------------------------------------------------------

using System;
using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using ESI.NET;
using ESI.NET.Enumerations;
using ESI.NET.Models.Character;
using ESI.NET.Models.SSO;
using EVEDataUtils;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SMT.EVEData
{
    /// <summary>
    /// The main EVE Manager
    /// </summary>
    public class EveManager
    {
        /// <summary>
        /// singleton instance of this class
        /// 此类的singleton实例
        /// </summary>
        private static EveManager instance;

        private bool BackgroundThreadShouldTerminate;

        /// <summary>
        /// Read position map for the intel files
        /// 读取情报文件的位置图
        /// </summary>
        private Dictionary<string, int> intelFileReadPos;

        /// <summary>
        /// Read position map for the intel files
        /// 读取情报文件的位置图
        /// </summary>
        private Dictionary<string, int> gameFileReadPos;

        /// <summary>
        /// Read position map for the intel files
        /// 读取情报文件的位置图
        /// </summary>
        private Dictionary<string, string> gamelogFileCharacterMap;

        /// <summary>
        /// File system watcher
        /// 文件系统观察程序
        /// </summary>
        private FileSystemWatcher intelFileWatcher;

        /// <summary>
        /// File system watcher
        /// 文件系统观察程序
        /// </summary>
        private FileSystemWatcher gameLogFileWatcher;

        private string VersionStr;

        private bool WatcherThreadShouldTerminate;

        private TimeSpan CharacterUpdateRate = TimeSpan.FromSeconds(1);
        private TimeSpan LowFreqUpdateRate = TimeSpan.FromMinutes(20);
        private TimeSpan SOVCampaignUpdateRate = TimeSpan.FromSeconds(30);

        private DateTime NextCharacterUpdate = DateTime.MinValue;
        private DateTime NextLowFreqUpdate = DateTime.MinValue;
        private DateTime NextSOVCampaignUpdate = DateTime.MinValue;
        private DateTime NextDotlanUpdate = DateTime.MinValue;
        private DateTime LastDotlanUpdate = DateTime.MinValue;
        private string LastDotlanETAG = "";


        /// <summary>
        /// Initializes a new instance of the <see cref="EveManager" /> class
        /// <see cref="EveManager" /> 类的新实例初始化
        /// </summary>
        public EveManager(string version)
        {
            LocalCharacters = new List<LocalCharacter>();
            VersionStr = version;

            string SaveDataRoot = EveAppConfig.StorageRoot;
            if (!Directory.Exists(SaveDataRoot))
            {
                Directory.CreateDirectory(SaveDataRoot);
            }

            DataRootFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

            SaveDataRootFolder = SaveDataRoot;

            SaveDataVersionFolder = EveAppConfig.VersionStorage;
            if (!Directory.Exists(SaveDataVersionFolder))
            {
                Directory.CreateDirectory(SaveDataVersionFolder);
            }

            string characterSaveFolder = Path.Combine(SaveDataRootFolder, "Portraits");
            if (!Directory.Exists(characterSaveFolder))
            {
                Directory.CreateDirectory(characterSaveFolder);
            }

            CharacterIDToName = new SerializableDictionary<int, string>();
            AllianceIDToName = new SerializableDictionary<int, string>();
            AllianceIDToTicker = new SerializableDictionary<int, string>();
            NameToSystem = new Dictionary<string, System>();
            IDToSystem = new Dictionary<long, System>();

            ServerInfo = new EVEData.Server();
            Coalitions = new List<Coalition>();
        }

        /// <summary>
        /// Intel Updated Event Handler
        /// 信息更新事件处理
        /// </summary>
        public delegate void IntelUpdatedEventHandler(List<IntelData> idl);

        /// <summary>
        /// Intel Updated Event
        /// 信息更新事件
        /// </summary>
        public event IntelUpdatedEventHandler IntelUpdatedEvent;

        /// <summary>
        /// GameLog Added Event Handler
        /// 游戏日志新增信息处理
        /// </summary>
        public delegate void GameLogAddedEventHandler(List<GameLogData> gll);

        /// <summary>
        /// Intel Added Event
        /// 信息添加事件
        /// </summary>
        public event GameLogAddedEventHandler GameLogAddedEvent;

        /// <summary>
        /// Ship Decloak Event Handler
        /// 舰船解除隐形时间处理
        /// </summary>
        public delegate void ShipDecloakedEventHandler(string pilot, string text);

        /// <summary>
        /// Ship Decloaked
        /// 舰船解除隐形
        /// </summary>
        public event ShipDecloakedEventHandler ShipDecloakedEvent;

        /// <summary>
        /// Combat Event Handler
        /// 战斗时间处理
        /// </summary>
        public delegate void CombatEventHandler(string pilot, string text);

        /// <summary>
        /// Combat Events
        /// 战斗事件
        /// </summary>
        public event CombatEventHandler CombatEvent;
        
        /*
         * JumpShip
         * 旗舰跳舰船类型
         * Dread  Dreadnought 无畏舰
         * Carrier 航母
         * FAX  Force Auxiliary  后勤航
         * Super  超级航母
         * Titan  泰坦
         * Blops  Black Ops  黑隐
         * JF  Jump Freighter  跳货
         * Rorqual  矿船/大鱼
         * HW Humpback whale 座头鲸
         */

        public enum JumpShip
        {
            Dread,
            Carrier,
            FAX,
            Super,
            Titan,
            Blops,
            JF,
            Rorqual,
            HW,
        }

        /// <summary>
        /// Gets or sets the Singleton Instance of the EVEManager
        /// 获取或设置EVEManager的单例实例
        /// </summary>
        public static EveManager Instance
        {
            get
            {
                return instance;
            }

            set
            {
                EveManager.instance = value;
            }
        }

        public string EVELogFolder { get; set; }

        /// <summary>
        /// Sov Campaign Updated Event Handler
        /// Sov Campaign 更新事件处理
        /// </summary>
        public delegate void SovCampaignUpdatedHandler();

        /// <summary>
        /// Sov Campaign updated Added Events
        /// Sov Campaign 更新事件
        /// </summary>
        public event SovCampaignUpdatedHandler SovUpdateEvent;

        /// <summary>
        /// Thera Connections Updated Event Handler
        /// 希拉连接更新事件处理
        /// </summary>
        public delegate void TheraUpdatedHandler();

        /// <summary>
        /// Thera Updated Added Events
        /// 希拉更新事件
        /// </summary>
        public event TheraUpdatedHandler TheraUpdateEvent;

        /// <summary>
        /// Storms Updated Event Handler
        /// 风暴更新事件处理
        /// </summary>
        public delegate void StormsUpdatedHandler();

        /// <summary>
        /// Storms Updated Added Events
        /// 风暴更新事件
        /// </summary>
        public event StormsUpdatedHandler StormsUpdateEvent;

        /// <summary>
        /// Local Characters Updated Event Handler
        /// 本地角色更新事件处理
        /// </summary>
        public delegate void LocalCharactersUpdatedHandler();

        /// <summary>
        /// Local Characters Updated Events
        /// 本地角色更新事件
        /// </summary>
        public event LocalCharactersUpdatedHandler LocalCharacterUpdateEvent;

        public List<SOVCampaign> ActiveSovCampaigns { get; set; }

        /// <summary>
        /// Gets or sets the Alliance ID to Name dictionary
        /// 获取或设置联盟ID到名称的字典
        /// </summary>
        public SerializableDictionary<int, string> AllianceIDToName { get; set; }

        /// <summary>
        /// Gets or sets the Alliance ID to Alliance Ticker dictionary
        /// 获取或设置联盟ID到联盟标记的字典
        /// </summary>
        public SerializableDictionary<int, string> AllianceIDToTicker { get; set; }

        /// <summary>
        /// Gets or sets the character cache
        /// 获取或设置角色缓存
        /// </summary>
        [XmlIgnoreAttribute]
        public SerializableDictionary<string, Character> CharacterCache { get; set; }

        /// <summary>
        /// Gets or sets the Alliance ID to Name dictionary
        /// 获取或设置联盟ID到名称的字典
        /// </summary>
        public SerializableDictionary<int, string> CharacterIDToName { get; set; }

        public List<Coalition> Coalitions { get; set; }

        public ESI.NET.EsiClient ESIClient { get; set; }

        public List<string> ESIScopes { get; set; }

        /// <summary>
        /// Gets or sets the Intel List
        /// 获取或设置情报列表
        /// </summary>
        public FixedQueue<EVEData.IntelData> IntelDataList { get; set; }

        /// <summary>
        /// Gets or sets the Gamelog List
        /// 获取或设置游戏日志列表
        /// </summary>
        public FixedQueue<EVEData.GameLogData> GameLogList { get; set; }

        /// <summary>
        /// Gets or sets the current list of Jump Bridges
        /// 获取或设置当前的跳桥列表
        /// </summary>
        public List<JumpBridge> JumpBridges { get; set; }

        /// <summary>
        /// Gets or sets the list of Characters we are tracking
        /// 获取或设置我们正在跟踪的角色列表
        /// </summary>
        [XmlIgnoreAttribute]
        public List<LocalCharacter> LocalCharacters { get; set; }

        /// <summary>
        /// Gets or sets the list of Faction warfare systems
        /// 获取或设置阵营战争系统列表
        /// </summary>
        [XmlIgnoreAttribute]
        public List<FactionWarfareSystemInfo> FactionWarfareSystems { get; set; }

        /// <summary>
        /// Gets or sets the master list of Regions
        /// 获取或设置区域的主列表
        /// </summary>
        public List<MapRegion> Regions { get; set; }


        /// <summary>
        /// Location of the static data distributed with the exectuable
        /// 本地静态数据的位置
        /// </summary>
        public string DataRootFolder { get; set; }

        /// <summary>
        /// Gets or sets the folder to cache dotland svg's etc to
        /// 获取或设置缓存dotland svg等的文件夹
        /// </summary>
        public string SaveDataRootFolder { get; set; }

        /// <summary>
        /// Gets or sets the folder to cache dotland svg's etc to
        /// 获取或设置缓存dotland svg等的文件夹
        /// </summary>
        public string SaveDataVersionFolder { get; set; }

        public EVEData.Server ServerInfo { get; set; }

        /// <summary>
        /// Gets or sets the ShipTypes ID to Name dictionary
        /// 获取或设置船舶类型ID到名称的字典
        /// </summary>
        public SerializableDictionary<string, string> ShipTypes { get; set; }

        /// <summary>
        /// Gets or sets the System ID to Name dictionary
        /// 获取或设置系统ID到名称的字典
        /// </summary>
        public SerializableDictionary<long, string> SystemIDToName { get; set; }

        /// <summary>
        /// Gets or sets the master List of Systems
        /// 获取或设置星系的主列表
        /// </summary>
        public List<System> Systems { get; set; }

        /// <summary>
        /// Gets or sets the current list of thera connections
        /// 获取或设置当前的希拉连接列表
        /// </summary>
        public List<TheraConnection> TheraConnections { get; set; }

        public bool UseESIForCharacterPositions { get; set; }

        public List<Storm> MetaliminalStorms { get; set; }

        public List<POI> PointsOfInterest { get; set; }

        /// <summary>
        /// Gets or sets the current list of ZKillData
        /// 获取或设置当前的ZKillData列表
        /// </summary>
        public ZKillRedisQ ZKillFeed { get; set; }

        /// <summary>
        /// Gets or sets the current list of clear markers for the intel (eg "Clear" "Clr" etc)
        /// 获取或设置情报的清除标记列表（例如“Clear”“Clr”等）
        /// </summary>
        public List<string> IntelClearFilters { get; set; }

        /// <summary>
        /// Gets or sets the current list of intel filters used to monitor the local log files
        /// 获取或设置用于监视本地日志文件的情报过滤器列表
        /// </summary>
        public List<string> IntelFilters { get; set; }

        /// <summary>
        /// Gets or sets the current list of ignore markers for the intel (eg "status")
        /// 获取或设置情报的忽略标记列表（例如“status”）
        /// </summary>
        public List<string> IntelIgnoreFilters { get; set; }

        /// <summary>
        /// Gets or sets the current list of alerting intel market for intel (eg "pilot538 Maken")
        /// 获取或设置情报的警报情报市场（例如“pilot538 Maken”）
        /// </summary>
        public List<string> IntelAlertFilters { get; set; }

        /// <summary>
        /// Gets or sets the Name to System dictionary
        /// 获取或设置名称到星系的字典
        /// </summary>
        private Dictionary<string, System> NameToSystem { get; }

        /// <summary>
        /// Gets or sets the ID to System dictionary
        /// 获取或设置ID到星系的字典
        /// </summary>
        private Dictionary<long, System> IDToSystem { get; }

        /// <summary>
        /// Scrape the maps from dotlan and initialise the region data from dotlan
        /// 从dotlan中抓取地图并从dotlan初始化区域数据
        /// </summary>
        public void CreateFromScratch(string sourceFolder, string outputFolder)
        {
            // allow parsing to work for all locales (comma/dot in csv float)
            // 允许解析所有区域（csv浮点数中的逗号/点）
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Regions = new List<MapRegion>();

            // manually add the regions we care about
            // 手动添加我们关心的区域
            Regions.Add(new MapRegion("艾里迪亚", "10000054", "Amarr", 280, 810));
            Regions.Add(new MapRegion("暗涌之域", "10000069", "Caldari", 900, 500));
            Regions.Add(new MapRegion("幽暗之域", "10000038", "Amarr", 1000, 920));
            Regions.Add(new MapRegion("血脉", "10000055", string.Empty, 1040, 100));
            Regions.Add(new MapRegion("地窖", "10000007", string.Empty, 1930, 800));
            Regions.Add(new MapRegion("卡彻", "10000014", string.Empty, 1110, 1280));
            Regions.Add(new MapRegion("赛塔德洱", "10000033", "Caldari", 1010, 620));
            Regions.Add(new MapRegion("云环", "10000051", string.Empty, 500, 240));
            Regions.Add(new MapRegion("钴蓝边域", "10000053", string.Empty, 1900, 130));
            Regions.Add(new MapRegion("柯尔斯", "10000012", "Angel Cartel", 1350, 1120));
            Regions.Add(new MapRegion("德克廉", "10000035", string.Empty, 820, 150));
            Regions.Add(new MapRegion("绝地之域", "10000060", "Blood Raider", 230, 1210));
            Regions.Add(new MapRegion("德里克", "10000001", "Ammatar", 1300, 970));
            Regions.Add(new MapRegion("底特里德", "10000005", string.Empty, 1760, 1400));
            Regions.Add(new MapRegion("破碎", "10000036", "Amarr", 990, 1060));
            Regions.Add(new MapRegion("多美", "10000043", "Amarr", 810, 960));
            Regions.Add(new MapRegion("埃索特亚", "10000039", string.Empty, 880, 1450));
            Regions.Add(new MapRegion("精华之域", "10000064", "Gallente", 740, 580));
            Regions.Add(new MapRegion("琉蓝之穹", "10000027", string.Empty, 1570, 620));
            Regions.Add(new MapRegion("埃维希尔", "10000037", "Gallente", 660, 730));
            Regions.Add(new MapRegion("斐德", "10000046", string.Empty, 720, 260));
            Regions.Add(new MapRegion("非塔波利斯", "10000056", string.Empty, 1070, 1510));
            Regions.Add(new MapRegion("伏尔戈", "10000002", "Caldari", 1200, 620));
            Regions.Add(new MapRegion("源泉之域", "10000058", string.Empty, 120, 500));
            Regions.Add(new MapRegion("对舞之域", "10000029", "The Society", 1330, 490));
            Regions.Add(new MapRegion("吉勒西斯", "10000067", "Amarr", 480, 860));
            Regions.Add(new MapRegion("大荒野", "10000011", "Thukker Tribe", 1630, 920));
            Regions.Add(new MapRegion("西玛特尔", "10000030", "Minmatar", 1220, 860));
            Regions.Add(new MapRegion("伊梅瑟亚", "10000025", string.Empty, 1350, 1230));
            Regions.Add(new MapRegion("绝径", "10000031", string.Empty, 1200, 1390));
            Regions.Add(new MapRegion("因斯姆尔", "10000009", string.Empty, 1880, 1160));
            Regions.Add(new MapRegion("卡多尔", "10000052", "Amarr", 660, 880));
            Regions.Add(new MapRegion("卡勒瓦拉阔地", "10000034", string.Empty, 1490, 370));
            Regions.Add(new MapRegion("卡尼迪", "10000049", "Khanid", 470, 1140));
            Regions.Add(new MapRegion("柯埃佐", "10000065", "Amarr", 500, 1010));
            Regions.Add(new MapRegion("长征", "10000016", "Caldari", 1100, 460));
            Regions.Add(new MapRegion("糟粕之域", "10000013", string.Empty, 1770, 520));
            Regions.Add(new MapRegion("美特伯里斯", "10000042", "Minmatar", 1330, 730));
            Regions.Add(new MapRegion("摩登赫斯", "10000028", "Minmatar", 1460, 860));
            Regions.Add(new MapRegion("欧莎", "10000040", string.Empty, 1890, 320));
            Regions.Add(new MapRegion("欧米斯特", "10000062", string.Empty, 1440, 1480));
            Regions.Add(new MapRegion("域外走廊", "10000021", string.Empty, 1930, 460));
            Regions.Add(new MapRegion("外环", "10000057", "ORE", 240, 280));
            Regions.Add(new MapRegion("摄魂之域", "10000059", string.Empty, 640, 1480));
            Regions.Add(new MapRegion("贝斯", "10000063", string.Empty, 440, 1400));
            Regions.Add(new MapRegion("佩利根弗", "10000066", string.Empty, 1600, 260));
            Regions.Add(new MapRegion("宁静之域", "10000048", "Gallente", 600, 440));
            Regions.Add(new MapRegion("普罗维登斯", "10000047", string.Empty, 1010, 1130));
            Regions.Add(new MapRegion("黑渊", "10000023", string.Empty, 870, 380));
            Regions.Add(new MapRegion("逑瑞斯", "10000050", string.Empty, 680, 1280));
            Regions.Add(new MapRegion("灼热之径", "10000008", string.Empty, 1600, 1080));
            Regions.Add(new MapRegion("金纳泽", "10000032", "Gallente", 950, 770));
            Regions.Add(new MapRegion("孤独之域", "10000044", "Gallente", 310, 670));
            Regions.Add(new MapRegion("螺旋之域", "10000018", string.Empty, 1720, 700));
            Regions.Add(new MapRegion("混浊", "10000022", "Sansha", 900, 1350));
            Regions.Add(new MapRegion("辛迪加", "10000041", "Syndicate", 360, 500));
            Regions.Add(new MapRegion("塔什蒙贡", "10000020", "Amarr", 730, 1090));
            Regions.Add(new MapRegion("特纳", "10000045", string.Empty, 1400, 140));
            Regions.Add(new MapRegion("特里菲斯", "10000061", string.Empty, 1430, 1350));
            Regions.Add(new MapRegion("特布特", "10000010", string.Empty, 1070, 290));
            Regions.Add(new MapRegion("静寂谷", "10000003", string.Empty, 1230, 380));
            Regions.Add(new MapRegion("维纳尔", "10000015", "Guristas", 1140, 210));
            Regions.Add(new MapRegion("维格温铎", "10000068", "Gallente", 490, 660));
            Regions.Add(new MapRegion("邪恶湾流", "10000006", string.Empty, 1580, 1230));
            Regions.Add(new MapRegion("波赫文", "10000008", "Triglavian", 50, 50));
            
            Regions.Add(new MapRegion("战争地带 - 艾玛 vs 米玛塔尔", "", "Faction War", 50, 120, true));
            Regions.Add(new MapRegion("战争地带 - 加达里 vs 盖伦特", "", "Faction War", 50, 190, true));
            Regions.Add(new MapRegion("赞颂之域", "10001000", string.Empty, 50, 260));

            SystemIDToName = new SerializableDictionary<long, string>();

            Systems = new List<System>();

            // update the region cache
            // 更新区域缓存
            foreach (MapRegion rd in Regions)
            {


                string localSVG = sourceFolder + @"data\SourceMaps\raw\" + rd.DotLanRef + "_layout.svg";
                // Console.WriteLine(localSVG);
                if (!File.Exists(localSVG))
                {
                    // error
                    // 错误
                    throw new NullReferenceException();
                }

                // parse the svg as xml
                // 将svg解析为xml
                XmlDocument xmldoc = new XmlDocument
                {
                    XmlResolver = null
                };
                FileStream fs = new FileStream(localSVG, FileMode.Open, FileAccess.Read);
                xmldoc.Load(fs);

                // get the svg/g/g sys use child nodes
                // 获取svg/g/g sys使用子节点
                string systemsXpath = @"//*[@Type='system']";
                XmlNodeList xnl = xmldoc.SelectNodes(systemsXpath);

                foreach (XmlNode xn in xnl)
                {
                    long systemID = long.Parse(xn.Attributes["ID"].Value);
                    float x = float.Parse(xn.Attributes["x"].Value);
                    float y = float.Parse(xn.Attributes["y"].Value);

                    float RoundVal = 10.0f;
                    x = (float)Math.Round(x / RoundVal, 0) * RoundVal;
                    y = (float)Math.Round(y / RoundVal, 0) * RoundVal;

                    string name;
                    string region;

                    if (xn.Attributes["Name"] == null)
                    {
                        name = GetEveSystemFromID(systemID).Name;
                        region = GetEveSystemFromID(systemID).Region;
                    }
                    else
                    {
                        name = xn.Attributes["Name"].Value;
                        region = xn.Attributes["Region"].Value;
                    }

                    bool hasStation = false;
                    bool hasIceBelt = false;

                    // create and add the system
                    // 创建并添加星系
                    if (region == rd.Name)
                    {
                        System s = new System(name, systemID, rd.Name, hasStation, hasIceBelt);
                        if (GetEveSystem(name) != null)
                        {
                            int test = 0;
                            test++;
                        }
                        Systems.Add(s);
                        NameToSystem[name] = s;
                        IDToSystem[systemID] = s;
                    }

                    // create and add the map version
                    // 创建并添加地图版本
                    rd.MapSystems[name] = new MapSystem
                    {
                        Name = name,
                        Layout = new Vector2(x, y),
                        Region = region,
                        OutOfRegion = rd.Name != region,
                    };
                }
            }

            // now open up the eve static data export and extract some info from it
            // 现在打开EVE静态数据导出并从中提取一些信息
            string eveStaticDataSolarSystemFile = sourceFolder + @"\data\mapSolarSystems.csv";
            if (File.Exists(eveStaticDataSolarSystemFile))
            {
                StreamReader file = new StreamReader(eveStaticDataSolarSystemFile);

                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    string[] bits = line.Split(',');

                    string regionID = bits[0];
                    string constID = bits[1];
                    string systemID = bits[2];
                    string systemName = bits[3]; // SystemIDToName[SystemID];

                    //CCP have their own version of what a Light Year is.. so instead of 9460730472580800.0 its this
                    // beware when converting units
                    // CCP有自己的光年版本。所以，不是9460730472580800.0，而是这个
                    // 转换单位时要小心
                    decimal LYScale = 9460000000000000.0m;

                    decimal x = Convert.ToDecimal(bits[4]);
                    decimal y = Convert.ToDecimal(bits[5]);
                    decimal z = Convert.ToDecimal(bits[6]);
                    double security = Convert.ToDouble(bits[21]);
                    double radius = Convert.ToDouble(bits[23]);

                    System s = GetEveSystem(systemName);
                    if (s != null)
                    {
                        // note : scale the coordinates to Light Year scale as at M double doesnt have enough precision however decimal doesnt
                        // have the range for the calculations
                        // 注意：将坐标缩放到光年比例，因为在M double中没有足够的精度，但是十进制数没有
                        // 有计算的范围
                        s.ActualX = x / LYScale;
                        s.ActualY = y / LYScale;
                        s.ActualZ = z / LYScale;
                        s.TrueSec = security;
                        s.ConstellationID = constID;
                        s.RadiusAU = radius / 149597870700;

                        // manually patch pochven
                        // 手动修补pochven
                        if (regionID == "10000070")
                        {
                            s.Region = "波赫文";
                        }
                    }

                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }

            // now open up the eve static data export for the regions and extract some info from it
            // 现在打开EVE静态数据导出的区域并从中提取一些信息
            string eveStaticDataRegionFile = sourceFolder + @"\data\mapRegions.csv";
            if (File.Exists(eveStaticDataRegionFile))
            {
                StreamReader file = new StreamReader(eveStaticDataRegionFile);

                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    string[] bits = line.Split(',');

                    string regionName = bits[1]; // SystemIDToName[SystemID]; 星系ID到名称

                    double x = Convert.ToDouble(bits[2]);
                    double y = Convert.ToDouble(bits[3]);
                    double z = Convert.ToDouble(bits[4]);

                    MapRegion r = GetRegion(regionName);
                    if (r != null)
                    {
                        r.RegionX = x / 9460730472580800.0;
                        r.RegionY = z / 9460730472580800.0;
                    }
                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }

            string eveStaticDataJumpsFile = sourceFolder + @"\data\mapSolarSystemJumps.csv";
            if (File.Exists(eveStaticDataJumpsFile))
            {
                StreamReader file = new StreamReader(eveStaticDataJumpsFile);

                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    string[] bits = line.Split(',');

                    long fromID = long.Parse(bits[2]);
                    long toID = long.Parse(bits[3]);

                    System from = GetEveSystemFromID(fromID);
                    System to = GetEveSystemFromID(toID);

                    if (from != null && to != null)
                    {
                        if (!from.Jumps.Contains(to.Name))
                        {
                            from.Jumps.Add(to.Name);
                        }
                        if (!to.Jumps.Contains(from.Name))
                        {
                            to.Jumps.Add(from.Name);
                        }
                    }
                }
            }

            string eveStaticDataJumpsExtraFile = sourceFolder + @"\data\mapSolarSystemJumpsExtra.csv";
            if (File.Exists(eveStaticDataJumpsExtraFile))
            {
                StreamReader file = new StreamReader(eveStaticDataJumpsExtraFile);

                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    string[] bits = line.Split(',');

                    string fromName = bits[0];
                    string toName = bits[1];

                    System from = GetEveSystem(fromName);
                    System to = GetEveSystem(toName);

                    if (from != null && to != null)
                    {
                        if (!from.Jumps.Contains(to.Name))
                        {
                            from.Jumps.Add(to.Name);
                        }
                        if (!to.Jumps.Contains(from.Name))
                        {
                            to.Jumps.Add(from.Name);
                        }
                    }
                }
            }

            // now open up the eve static data export and extract some info from it
            // 现在打开EVE静态数据导出并从中提取一些信息
            string eveStaticDataStationsFile = sourceFolder + @"\data\staStations.csv";
            if (File.Exists(eveStaticDataStationsFile))
            {
                StreamReader file = new StreamReader(eveStaticDataStationsFile);

                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    string[] bits = line.Split(',');

                    long stationSystem = long.Parse(bits[8]);

                    System SS = GetEveSystemFromID(stationSystem);
                    if (SS != null)
                    {
                        SS.HasNPCStation = true;
                    }
                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }

            // now open up the eve static data export and extract some info from it
            // 现在打开EVE静态数据导出并从中提取一些信息
            string eveStaticDataConstellationFile = sourceFolder + @"\data\mapConstellations.csv";
            if (File.Exists(eveStaticDataConstellationFile))
            {
                StreamReader file = new StreamReader(eveStaticDataConstellationFile);

                Dictionary<string, string> constMap = new Dictionary<string, string>();

                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    string[] bits = line.Split(',');

                    string constID = bits[1];
                    string constName = bits[2];

                    constMap[constID] = constName;
                }

                // TEMP : Manually add 
                // 临时：手动添加
                constMap["20010000"] = "绝望之地";

                foreach (System s in Systems)
                { 
                    s.ConstellationName = constMap[s.ConstellationID];
                }
                
            }
            else
            {
                throw new Exception("Data Creation Error");
            }

            // now open up the ice systems
            // 现在打开有冰带的星系
            string iceSystemsFile = sourceFolder + @"\data\iceSystems.csv";
            if (File.Exists(iceSystemsFile))
            {
                StreamReader file = new StreamReader(iceSystemsFile);
                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    System s = GetEveSystem(line);
                    if (s != null)
                    {
                        s.HasIceBelt = true;
                    }
                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }

            // now open up the ice systems
            // 现在打开有冰带的星系
            string fwSystemsFile = sourceFolder + @"\data\factionWarfareSystems.csv";
            if (File.Exists(fwSystemsFile))
            {
                StreamReader file = new StreamReader(fwSystemsFile);
                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    System s = GetEveSystem(line);
                    if (s != null)
                    {
                        s.FactionWarSystem = true;
                    }
                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }

            // now open up the blue a0 sun systems
            // 现在打开有蓝色a0太阳的星系
            string blueSunSystemsFile = sourceFolder + @"\data\a0BlueStarSystems.csv";
            if (File.Exists(blueSunSystemsFile))
            {
                StreamReader file = new StreamReader(blueSunSystemsFile);
                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    System s = GetEveSystem(line);
                    if (s != null)
                    {
                        s.HasBlueA0Star = true;
                    }
                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }

            foreach (System s in Systems)
            {
                NameToSystem[s.Name] = s;
                IDToSystem[s.ID] = s;

                // default to no invasion
                // 默认为无入侵
                s.TrigInvasionStatus = System.EdenComTrigStatus.None;
            }

            string trigSystemsFile = sourceFolder + @"\data\trigInvasionSystems.csv";
            if (File.Exists(trigSystemsFile))
            {
                StreamReader file = new StreamReader(trigSystemsFile);

                // read the headers..
                // 读取标题
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    string[] bits = line.Split(',');

                    string systemid = bits[0];
                    string status = bits[1];

                    System.EdenComTrigStatus invasionStatus = System.EdenComTrigStatus.None;
                    switch (status)
                    {
                        case "edencom_minor_victory":
                            invasionStatus = System.EdenComTrigStatus.EdencomMinorVictory;
                            break;

                        case "fortress":
                            invasionStatus = System.EdenComTrigStatus.Fortress;
                            break;

                        case "triglavian_minor_victory":
                            invasionStatus = System.EdenComTrigStatus.TriglavianMinorVictory;
                            break;
                    }

                    if (invasionStatus != System.EdenComTrigStatus.None)
                    {
                        System s = GetEveSystemFromID(long.Parse(systemid));
                        if (s != null)
                        {
                            s.TrigInvasionStatus = invasionStatus;
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }

            // now create the voronoi regions
            // 现在创建Voronoi区域
            foreach (MapRegion mr in Regions)
            {
                // enforce a minimum spread
                // 强制最小传播
                bool mrDone = false;
                int mrIteration = 0;
                float mrMinSpread = 49.0f;

                while (!mrDone)
                {
                    mrIteration++;
                    bool movedThisTime = false;

                    foreach (MapSystem sysA in mr.MapSystems.Values)
                    {
                        foreach (MapSystem sysB in mr.MapSystems.Values)
                        {
                            if (sysA == sysB)
                            {
                                continue;
                            }

                            float dx = sysA.Layout.X - sysB.Layout.X;
                            float dy = sysA.Layout.Y - sysB.Layout.Y;
                            float l = (float)Math.Sqrt(dx * dx + dy * dy);

                            float s = mrMinSpread - l;

                            if (s > 0)
                            {
                                movedThisTime = true;

                                // move apart
                                // 分开
                                dx = dx / l;
                                dy = dy / l;

                                sysB.Layout = new Vector2(sysB.Layout.X - (dx * s / 2), sysB.Layout.Y - (dy * s / 2));
                                sysA.Layout = new Vector2(sysA.Layout.X + (dx * s / 2), sysA.Layout.Y + (dy * s / 2));
                            }
                        }
                    }

                    if (movedThisTime == false)
                    {
                        mrDone = true;
                    }

                    if (mrIteration > 20)
                    {
                        mrDone = true;
                    }
                }

                // collect the system points to generate them from
                // 收集系统点以生成它们
                List<Vector2f> points = new List<Vector2f>();

                foreach (MapSystem ms in mr.MapSystems.Values.ToList())
                {
                    points.Add(new Vector2f(ms.Layout.X, ms.Layout.Y));
                }

                // generate filler points to help the voronoi to get better partitioning of open areas
                // 生成填充点以帮助voronoi更好地划分开放区域
                int division = 5;
                int minDistance = 100;
                int minDistanceOOR = 70;
                int margin = 180;

                List<Vector2f> fillerPoints = new List<Vector2f>();

                for (int ix = -margin; ix < 1050 + margin; ix += division)
                {
                    for (int iy = -margin; iy < 800 + margin; iy += division)
                    {
                        bool add = true;

                        foreach (MapSystem ms in mr.MapSystems.Values.ToList())
                        {
                            double dx = ms.Layout.X - ix;
                            double dy = ms.Layout.Y - iy;
                            double l = Math.Sqrt(dx * dx + dy * dy);

                            if (ms.OutOfRegion)
                            {
                                if (l < (minDistanceOOR))
                                {
                                    add = false;
                                    break;
                                }
                            }
                            else
                            {
                                if (l < (minDistance))
                                {
                                    add = false;
                                    break;
                                }
                            }
                        }

                        if (add)
                        {
                            fillerPoints.Add(new Vector2f(ix, iy));
                        }
                    }
                }
                points.AddRange(fillerPoints);

                Rectf clipRect = new Rectf(-margin, -margin, 1050 + 2 * margin, 800 + 2 * margin);

                // create the voronoi
                // 创建voronoi
                csDelaunay.Voronoi v = new csDelaunay.Voronoi(points, clipRect, 0);

                int i = 0;
                // extract the points from the graph for each cell
                // 从每个单元格的图中提取点
                foreach (MapSystem ms in mr.MapSystems.Values.ToList())
                {
                    csDelaunay.Site s = v.SitesIndexedByLocation[points[i]];
                    i++;

                    List<Vector2f> cellList = s.Region(clipRect);
                    ms.Layout = new Vector2(s.x, s.y);

                    ms.CellPoints = new List<Vector2>();

                    foreach (Vector2f vc in cellList)
                    {
                        float RoundVal = 2.5f;

                        double finalX = vc.x;
                        double finalY = vc.y;

                        int X = (int)(Math.Round(finalX / RoundVal, 1, MidpointRounding.AwayFromZero) * RoundVal);
                        int Y = (int)(Math.Round(finalY / RoundVal, 1, MidpointRounding.AwayFromZero) * RoundVal);

                        ms.CellPoints.Add(new Vector2(X, Y));
                    }
                }
            }

            foreach (MapRegion rr in Regions)
            {
                // link to the real systems
                // 链接到真实星系
                foreach (MapSystem ms in rr.MapSystems.Values.ToList())
                {
                    ms.ActualSystem = GetEveSystem(ms.Name);

                    if (!ms.OutOfRegion)
                    {
                        if (ms.ActualSystem.TrueSec >= 0.45)
                        {
                            rr.HasHighSecSystems = true;
                        }

                        if (ms.ActualSystem.TrueSec > 0.0 && ms.ActualSystem.TrueSec < 0.45)
                        {
                            rr.HasLowSecSystems = true;
                        }

                        if (ms.ActualSystem.TrueSec <= 0.0)
                        {
                            rr.HasNullSecSystems = true;
                        }
                    }

                    if (rr.MetaRegion)
                    {
                        ms.OutOfRegion = !ms.ActualSystem.FactionWarSystem;
                    }
                }
            }

            // collect the system points to generate them from
            // 收集星系点以生成它们
            List<Vector2f> regionpoints = new List<Vector2f>();

            // now Generate the region links
            // 现在生成区域链接
            foreach (MapRegion mr in Regions)
            {
                mr.RegionLinks = new List<string>();

                regionpoints.Add(new Vector2f(mr.UniverseViewX, mr.UniverseViewY));

                foreach (MapSystem ms in mr.MapSystems.Values.ToList())
                {
                    // only check systems in the region
                    // 只检查区域内的星系
                    if (ms.ActualSystem.Region == mr.Name)
                    {
                        foreach (string s in ms.ActualSystem.Jumps)
                        {
                            System sys = GetEveSystem(s);

                            // we have link to another region
                            // 我们有到另一个区域的链接
                            if (sys.Region != mr.Name)
                            {
                                if (!mr.RegionLinks.Contains(sys.Region))
                                {
                                    mr.RegionLinks.Add(sys.Region);
                                }
                            }
                        }
                    }
                }
            }

            // now get the ships
            // 现在获取船只
            string eveStaticDataItemTypesFile = sourceFolder + @"\data\invTypes.csv";
            if (File.Exists(eveStaticDataItemTypesFile))
            {
                ShipTypes = new SerializableDictionary<string, string>();

                List<string> ValidShipGroupIDs = new List<string>();

                ValidShipGroupIDs.Add("25"); //  Frigate 护卫舰
                ValidShipGroupIDs.Add("26"); //  Cruiser 巡洋舰
                ValidShipGroupIDs.Add("27"); //  Battleship 战列舰
                ValidShipGroupIDs.Add("28"); //  Industrial 工业舰
                ValidShipGroupIDs.Add("29"); //  Capsule 太空舱
                ValidShipGroupIDs.Add("30"); //  Titan 泰坦
                ValidShipGroupIDs.Add("31"); //  Shuttle 穿梭机
                ValidShipGroupIDs.Add("237"); //  Corvette 新手船
                ValidShipGroupIDs.Add("324"); //  Assault Frigate 突击护卫舰
                ValidShipGroupIDs.Add("358"); //  Heavy Assault Cruiser 重型突击巡洋舰
                ValidShipGroupIDs.Add("380"); //  Deep Space Transport 深空运输舰
                ValidShipGroupIDs.Add("381"); //  Elite Battleship 精英战列舰
                ValidShipGroupIDs.Add("419"); //  Combat Battlecruiser 战斗巡洋舰
                ValidShipGroupIDs.Add("420"); //  Destroyer 驱逐舰
                ValidShipGroupIDs.Add("463"); //  Mining Barge 采矿驳船
                ValidShipGroupIDs.Add("485"); //  Dreadnought 无畏舰
                ValidShipGroupIDs.Add("513"); //  Freighter 运输舰
                ValidShipGroupIDs.Add("540"); //  Command Ship 指挥舰
                ValidShipGroupIDs.Add("541"); //  Interdictor 拦截舰
                ValidShipGroupIDs.Add("543"); //  Exhumer 采矿船
                ValidShipGroupIDs.Add("547"); //  Carrier 航母
                ValidShipGroupIDs.Add("659"); //  Supercarrier 超级航母
                ValidShipGroupIDs.Add("830"); //  Covert Ops 隐形特勤舰
                ValidShipGroupIDs.Add("831"); //  Interceptor 拦截舰
                ValidShipGroupIDs.Add("832"); //  Logistics 战术护卫舰
                ValidShipGroupIDs.Add("833"); //  Force Recon Ship 侦察舰
                ValidShipGroupIDs.Add("834"); //  Stealth Bomber 隐轰
                ValidShipGroupIDs.Add("883"); //  Capital Industrial Ship 旗舰级工业船
                ValidShipGroupIDs.Add("893"); //  Electronic Attack Ship 电子干扰舰
                ValidShipGroupIDs.Add("894"); //  Heavy Interdiction Cruiser 重型拦截巡洋舰
                ValidShipGroupIDs.Add("898"); //  Black Ops 黑隐特勤舰
                ValidShipGroupIDs.Add("900"); //  Marauder 掠夺者
                ValidShipGroupIDs.Add("902"); //  Jump Freighter 跳货
                ValidShipGroupIDs.Add("906"); //  Combat Recon Ship 战术侦察舰
                ValidShipGroupIDs.Add("941"); //  Industrial Command Ship 工业指挥舰
                ValidShipGroupIDs.Add("963"); //  Strategic Cruiser 战略巡洋舰
                ValidShipGroupIDs.Add("1022"); //  Prototype Exploration Ship 原型探测船
                ValidShipGroupIDs.Add("1201"); //  Attack Battlecruiser 攻击巡洋舰
                ValidShipGroupIDs.Add("1202"); //  Blockade Runner 封锁舰
                ValidShipGroupIDs.Add("1283"); //  Expedition Frigate 探险护卫舰
                ValidShipGroupIDs.Add("1305"); //  Tactical Destroyer 战术驱逐舰
                ValidShipGroupIDs.Add("1527"); //  Logistics Frigate 战术护卫舰
                ValidShipGroupIDs.Add("1534"); //  Command Destroyer 指挥驱逐舰
                ValidShipGroupIDs.Add("1538"); //  Force Auxiliary 舰队辅助舰
                ValidShipGroupIDs.Add("1972"); //  Flag Cruiser 旗舰巡洋舰
                
                // fighters
                // 战斗机
                ValidShipGroupIDs.Add("1537"); //  Support Fighter None    0   0   0   0   1 铁甲战斗机
                ValidShipGroupIDs.Add("1652"); //  Light Fighter   None    0   0   0   0   1 轻型战斗机
                ValidShipGroupIDs.Add("1653"); //  Heavy Fighter   None    0   0   0   0   1 重型战斗机

                // deployables
                // 部署物
                ValidShipGroupIDs.Add("361");  //  Mobile Warp Disruptor 移动式跃迁干扰器
                ValidShipGroupIDs.Add("1149"); //  Mobile Jump Disruptor 移动式跃迁干扰器
                ValidShipGroupIDs.Add("1246"); //  Mobile Depot 移动式仓库
                ValidShipGroupIDs.Add("1247"); //  Mobile Siphon Unit 移动式吸取装置
                ValidShipGroupIDs.Add("1249"); //  Mobile Cyno Inhibitor 移动式跃迁干扰器
                ValidShipGroupIDs.Add("1250"); //  Mobile Tractor Unit 移动式牵引装置
                ValidShipGroupIDs.Add("1273"); //  Encounter Surveillance System 遭遇监视系统
                ValidShipGroupIDs.Add("1274"); //  Mobile Decoy Unit 移动式诱饵装置
                ValidShipGroupIDs.Add("1275"); //  Mobile Scan Inhibitor 移动式扫描干扰器
                ValidShipGroupIDs.Add("1276"); //  Mobile Micro Jump Unit 移动式微型跃迁装置
                ValidShipGroupIDs.Add("1297"); //  Mobile Vault 移动式保险箱

                // structures
                // 结构
                ValidShipGroupIDs.Add("1312"); //  Observatory Structures 观测站
                ValidShipGroupIDs.Add("1404"); //  Engineering Complex 工程复合体
                ValidShipGroupIDs.Add("1405"); //  Laboratory 实验室
                ValidShipGroupIDs.Add("1406"); //  Refinery 炼油厂
                ValidShipGroupIDs.Add("1407"); //  Observatory Array 观测站
                ValidShipGroupIDs.Add("1408"); //  Stargate 星门
                ValidShipGroupIDs.Add("1409"); //  Administration Hub 管理中心
                ValidShipGroupIDs.Add("1410"); //  Advertisement Center 广告中心

                // citadels
                // 堡垒
                ValidShipGroupIDs.Add("1657"); //  Citadel 堡垒
                ValidShipGroupIDs.Add("1876"); //  Engineering Complex 工程复合体
                ValidShipGroupIDs.Add("1924"); //  Forward Operating Base 前沿作战基地

                StreamReader file = new StreamReader(eveStaticDataItemTypesFile);

                // read the headers..
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    string[] bits = line.Split(',');

                    if (bits.Length < 3)
                    {
                        continue;
                    }

                    string typeID = bits[0];
                    string groupID = bits[1];
                    string ItemName = bits[2];

                    if (ValidShipGroupIDs.Contains(groupID))
                    {
                        ShipTypes.Add(typeID, ItemName);
                    }
                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }

            // now add the jove systems
            // 现在添加朱庇特星系
            string eveStaticDataJoveObservatories = sourceFolder + @"\data\JoveSystems.csv";
            if (File.Exists(eveStaticDataJoveObservatories))
            {
                StreamReader file = new StreamReader(eveStaticDataJoveObservatories);

                // read the headers..
                string line;
                line = file.ReadLine();
                while ((line = file.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    string[] bits = line.Split(',');

                    if (bits.Length != 4)
                    {
                        continue;
                    }

                    string system = bits[0];

                    System s = GetEveSystem(system);
                    if (s != null)
                    {
                        s.HasJoveObservatory = true;
                    }
                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }


            // Now add the joveGate Systems
            // 现在添加朱庇特星系
            string eveStaticDataJoveGates = sourceFolder + @"\data\JoveGates.csv";
            if (File.Exists(eveStaticDataJoveGates))
            {
                StreamReader file = new StreamReader(eveStaticDataJoveGates);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    System s = GetEveSystem(line);
                    if (s != null)
                    {
                        s.HasJoveGate = true;
                    }
                }
            }
            else
            {
                throw new Exception("Data Creation Error");
            }



            // now generate the 2d universe view coordinates
            // 现在生成2D宇宙视图坐标

            double RenderSize = 5000;
            double universeXMin = 484452845697854000;
            double universeXMax = -484452845697854000;

            double universeZMin = 484452845697854000;
            double universeZMax = -472860102256057000.0;

            foreach (EVEData.System sys in Systems)
            {
                if ((double)sys.ActualX < universeXMin)
                {
                    universeXMin = (double)sys.ActualX;
                }

                if ((double)sys.ActualX > universeXMax)
                {
                    universeXMax = (double)sys.ActualX;
                }

                if ((double)sys.ActualZ < universeZMin)
                {
                    universeZMin = (double)sys.ActualZ;
                }

                if ((double)sys.ActualZ > universeZMax)
                {
                    universeZMax = (double)sys.ActualZ;
                }
            }
            double universeWidth = universeXMax - universeXMin;
            double universeDepth = universeZMax - universeZMin;
            double XScale = RenderSize / universeWidth;
            double ZScale = RenderSize / universeDepth;
            double universeScale = Math.Min(XScale, ZScale);

            foreach (EVEData.System sys in Systems)
            {
                double X = ((double)sys.ActualX - universeXMin) * universeScale;

                // need to invert Z
                double Z = (universeDepth - ((double)sys.ActualZ - universeZMin)) * universeScale;

                sys.UniverseX = X;
                sys.UniverseY = Z;
            }

            // now create the region outlines and recalc the centre
            // 现在创建区域轮廓并重新计算中心
            foreach (MapRegion mr in Regions)
            {
                mr.RegionX = (mr.RegionX - universeXMin) * universeScale;
                mr.RegionY = (universeDepth - (mr.RegionY - universeZMin)) * universeScale;

                List<nAlpha.Point> regionShapePL = new List<nAlpha.Point>();
                foreach (System s in Systems)
                {
                    if (s.Region == mr.Name)
                    {
                        nAlpha.Point p = new nAlpha.Point(s.UniverseX, s.UniverseY);
                        regionShapePL.Add(p);
                    }
                }

                nAlpha.AlphaShapeCalculator shapeCalc = new nAlpha.AlphaShapeCalculator();
                shapeCalc.Alpha = 1 / (20 * 5.22295244275827E-15);
                shapeCalc.CloseShape = true;

                nAlpha.Shape ns = shapeCalc.CalculateShape(regionShapePL.ToArray());

                mr.RegionOutline = new List<Vector2>();

                List<Tuple<int, int>> processed = new List<Tuple<int, int>>();

                int CurrentPoint = 0;
                int count = 0;
                int edgeCount = ns.Edges.Length;
                while (count < edgeCount)
                {
                    foreach (Tuple<int, int> i in ns.Edges)
                    {
                        if (processed.Contains(i))
                            continue;

                        if (i.Item1 == CurrentPoint)
                        {
                            mr.RegionOutline.Add(new Vector2((int)ns.Vertices[CurrentPoint].X, (int)ns.Vertices[CurrentPoint].Y));
                            CurrentPoint = i.Item2;
                            processed.Add(i);
                            break;
                        }

                        if (i.Item2 == CurrentPoint)
                        {
                            mr.RegionOutline.Add(new Vector2((int)ns.Vertices[CurrentPoint].X, (int)ns.Vertices[CurrentPoint].Y));
                            CurrentPoint = i.Item1;
                            processed.Add(i);
                            break;
                        }
                    }

                    count++;
                }
            }

            bool done = false;
            int iteration = 0;
            double minSpread = 19.0;

            while (!done)
            {
                iteration++;
                bool movedThisTime = false;

                foreach (EVEData.System sysA in Systems)
                {
                    foreach (EVEData.System sysB in Systems)
                    {
                        if (sysA == sysB)
                        {
                            continue;
                        }

                        double dx = sysA.UniverseX - sysB.UniverseX;
                        double dy = sysA.UniverseY - sysB.UniverseY;
                        double l = Math.Sqrt(dx * dx + dy * dy);

                        double s = minSpread - l;

                        if (s > 0)
                        {
                            movedThisTime = true;

                            // move apart
                            dx = dx / l;
                            dy = dy / l;

                            sysB.UniverseX -= dx * s / 2;
                            sysB.UniverseY -= dy * s / 2;

                            sysA.UniverseX += dx * s / 2;
                            sysA.UniverseY += dy * s / 2;
                        }
                    }
                }

                if (movedThisTime == false)
                {
                    done = true;
                }

                if (iteration > 20)
                {
                    done = true;
                }
            }

            // cache the navigation data
            // 缓存导航数据
            SerializableDictionary<string, List<string>> jumpRangeCache = Navigation.CreateStaticNavigationCache(Systems);

            // now serialise the classes to disk
            // 现在将类序列化到磁盘

            string saveDataFolder = outputFolder + @"\data\";

            Serialization.SerializeToDisk<SerializableDictionary<string, List<string>>>(jumpRangeCache, saveDataFolder + @"\JumpRangeCache.dat");
            Serialization.SerializeToDisk<SerializableDictionary<string, string>>(ShipTypes, saveDataFolder + @"\ShipTypes.dat");
            Serialization.SerializeToDisk<List<MapRegion>>(Regions, saveDataFolder + @"\MapLayout.dat");
            Serialization.SerializeToDisk<List<System>>(Systems, saveDataFolder + @"\Systems.dat");
        }

        /// <summary>
        /// Does the System Exist ?
        /// 系统是否存在 ?
        /// </summary>
        /// <param name="name">Name (not ID) of the system</param>
        public bool DoesSystemExist(string name) => GetEveSystem(name) != null;

        /// <summary>
        /// Get the alliance name from the alliance ID
        /// 获取联盟名称
        /// </summary>
        /// <param name="id">Alliance ID</param>
        /// <returns>Alliance Name</returns>
        public string GetAllianceName(int id)
        {
            string name = string.Empty;
            if (AllianceIDToName.ContainsKey(id))
            {
                name = AllianceIDToName[id];
            }

            return name;
        }

        /// <summary>
        /// Gets the alliance ticker eg "TEST" from the alliance ID
        /// 获取联盟标记
        /// </summary>
        /// <param name="id">Alliance ID</param>
        /// <returns>Alliance Ticker</returns>
        public string GetAllianceTicker(int id)
        {
            string ticker = string.Empty;
            if (AllianceIDToTicker.ContainsKey(id))
            {
                ticker = AllianceIDToTicker[id];
            }

            return ticker;
        }

        public string GetCharacterName(int id)
        {
            string name = string.Empty;
            if (CharacterIDToName.ContainsKey(id))
            {
                name = CharacterIDToName[id];
            }

            return name;
        }

        /// <summary>
        /// Get the ESI Logon URL String
        /// 获取ESI登录URL字符串
        /// </summary>
        public string GetESILogonURL()
        {
            string URL = CreateAuthenticationUrl(ESIScopes, VersionStr);
            return URL;
        }

        /// <summary>
        /// Get a System object from the name, note : for regions which have other region systems in it wont return
        /// 获取星系对象: 对于其中有其他区域系统的区域不会返回
        /// them.. eg TR07-s is on the esoteria map, but the object corresponding to the feythabolis map will be returned
        /// 例如 TR07-s 在埃索特里亚地图上，但将返回对应于费塞博利斯地图的对象
        /// </summary>
        /// <param name="name">Name (not ID) of the system</param>
        public System GetEveSystem(string name)
        {
            if (NameToSystem.ContainsKey(name))
            {
                return NameToSystem[name];
            }

            return null;
        }

        /// <summary>
        /// Get a System object from the ID
        /// 获取星系对象
        /// </summary>
        /// <param name="id">ID of the system</param>
        public System GetEveSystemFromID(long id)
        {
            if (IDToSystem.ContainsKey(id))
            {
                return IDToSystem[id];
            }

            return null;
        }

        /// <summary>
        /// Get a System name from the ID
        /// 获取星系名称
        /// </summary>
        /// <param name="id">ID of the system</param>
        public string GetEveSystemNameFromID(long id)
        {
            System s = GetEveSystemFromID(id);
            if (s != null)
            {
                return s.Name;
            }

            return string.Empty;
        }

        /// <summary>
        /// Calculate the range between the two systems
        /// 计算两个星系之间的范围
        /// </summary>
        public decimal GetRangeBetweenSystems(string from, string to)
        {
            System systemFrom = GetEveSystem(from);
            System systemTo = GetEveSystem(to);

            if (systemFrom == null || systemTo == null || from == to)
            {
                return 0.0M;
            }

            decimal x = systemFrom.ActualX - systemTo.ActualX;
            decimal y = systemFrom.ActualY - systemTo.ActualY;
            decimal z = systemFrom.ActualZ - systemTo.ActualZ;

            decimal length = DecimalMath.DecimalEx.Sqrt((x * x) + (y * y) + (z * z));

            return length;
        }

        /// <summary>
        /// Get the MapRegion from the name
        /// 获取星域名称
        /// </summary>
        /// <param name="name">Name of the Region</param>
        /// <returns>Region Object</returns>
        public MapRegion GetRegion(string name)
        {
            foreach (MapRegion reg in Regions)
            {
                if (reg.Name == name)
                {
                    return reg;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the System name from the System ID
        /// 获取星系名称
        /// </summary>
        /// <param name="id">System ID</param>
        /// <returns>System Name</returns>
        public string GetSystemNameFromSystemID(long id)
        {
            string name = string.Empty;
            if (SystemIDToName.ContainsKey(id))
            {
                name = SystemIDToName[id];
            }

            return name;
        }

        /// <summary>
        /// Hand the custom smtauth- url we get back from the logon screen
        /// 处理我们从登录屏幕获得的自定义smtauth- URL
        /// </summary>
        public async void HandleEveAuthSMTUri(Uri uri)
        {
            // parse the uri
            var query = HttpUtility.ParseQueryString(uri.Query);
            if (query["code"] == null)
            {
                // we're missing a query code
                return;
            }

            string code = query["code"];
            if (code.Contains("&"))
            {
                code = code.Split('&')[0];
            }
            SsoToken sst;

            try
            {
                sst = await GetToken(GrantType.AuthorizationCode, code);
                if (sst == null || sst.ExpiresIn == 0)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            AuthorizedCharacterData acd = await Verify(sst);

            acd.RefreshToken = sst.RefreshToken;
            acd.Token = sst.AccessToken;
            
            // now find the matching character and update..
            // 现在找到匹配的角色并更新
            LocalCharacter esiChar = null;
            foreach (LocalCharacter c in LocalCharacters)
            {
                if (c.Name == acd.CharacterName)
                {
                    esiChar = c;
                }
            }

            if (esiChar == null)
            {
                esiChar = new LocalCharacter(acd.CharacterName, string.Empty, string.Empty);
                LocalCharacters.Add(esiChar);

                if (LocalCharacterUpdateEvent != null)
                {
                    LocalCharacterUpdateEvent();
                }
            }
            
            // Token失效时间为10分钟
            acd.ExpiresOn = DateTime.UtcNow.AddSeconds(10 * 60);
            esiChar.ESIRefreshToken = acd.RefreshToken;
            esiChar.ESILinked = true;
            esiChar.ESIAccessToken = acd.Token;
            esiChar.ESIAccessTokenExpiry = acd.ExpiresOn;
            esiChar.ID = acd.CharacterID;
            esiChar.ESIAuthData = acd;

            // now to find if a matching character
            // 现在找到匹配的角色
        }

        public void InitNavigation()
        {
            SerializableDictionary<string, List<string>> jumpRangeCache;

            string DataRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

            string JRC = Path.Combine(DataRootPath, "JumpRangeCache.dat");

            if (!File.Exists(JRC))
            {
                throw new NotImplementedException();
            }
            jumpRangeCache = Serialization.DeserializeFromDisk<SerializableDictionary<string, List<string>>>(JRC);
            Navigation.InitNavigation(NameToSystem.Values.ToList(), JumpBridges, jumpRangeCache);

            InitZarzakhConnections();

        }

        /// <summary>
        /// Load the EVE Manager Data from Disk
        /// 加载磁盘上的EVE管理器数据
        /// </summary>
        public void LoadFromDisk()
        {
            SystemIDToName = new SerializableDictionary<long, string>();


            Regions = Serialization.DeserializeFromDisk<List<MapRegion>>(Path.Combine(DataRootFolder, "MapLayout.dat"));
            Systems = Serialization.DeserializeFromDisk<List<System>>(Path.Combine(DataRootFolder, "Systems.dat"));

            ShipTypes = Serialization.DeserializeFromDisk<SerializableDictionary<string, string>>(Path.Combine(DataRootFolder, "ShipTypes.dat"));

            foreach (System s in Systems)
            {
                SystemIDToName[s.ID] = s.Name;
            }

            CharacterIDToName = new SerializableDictionary<int, string>();
            AllianceIDToName = new SerializableDictionary<int, string>();
            AllianceIDToTicker = new SerializableDictionary<int, string>();

            // patch up any links
            foreach (System s in Systems)
            {
                NameToSystem[s.Name] = s;
                IDToSystem[s.ID] = s;
            }

            // now add the beacons
            string cynoBeaconsFile = Path.Combine(SaveDataRootFolder,  "CynoBeacons.txt");
            if (File.Exists(cynoBeaconsFile))
            {
                StreamReader file = new StreamReader(cynoBeaconsFile);

                string line;
                while ((line = file.ReadLine()) != null)
                {
                    string system = line.Trim();

                    System s = GetEveSystem(system);
                    if (s != null)
                    {
                        s.HasJumpBeacon = true;
                    }
                }
            }

            Init();
        }

        /// <summary>
        /// Load the jump bridge data from disk
        /// 加载磁盘上的跳桥数据
        /// </summary>
        public void LoadJumpBridgeData()
        {
            JumpBridges = new List<JumpBridge>();

            string dataFilename = Path.Combine(SaveDataRootFolder,  "JumpBridges_" + JumpBridge.SaveVersion + ".dat");
            if (!File.Exists(dataFilename))
            {
                return;
            }

            try
            {
                List<JumpBridge> loadList;
                XmlSerializer xms = new XmlSerializer(typeof(List<JumpBridge>));

                FileStream fs = new FileStream(dataFilename, FileMode.Open, FileAccess.Read);
                XmlReader xmlr = XmlReader.Create(fs);

                loadList = (List<JumpBridge>)xms.Deserialize(xmlr);

                foreach (JumpBridge j in loadList)
                {
                    JumpBridges.Add(j);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Update the Alliance and Ticker data for specified list
        /// 更新指定列表的联盟和标记数据
        /// </summary>
        public async Task ResolveAllianceIDs(List<int> IDs)
        {
            if (IDs.Count == 0)
            {
                return;
            }

            // strip out any ID's we already know..
            // 去除已知ID
            List<int> UnknownIDs = new List<int>();
            foreach (int l in IDs)
            {
                if (!AllianceIDToName.ContainsKey(l) || !AllianceIDToTicker.ContainsKey(l))
                {
                    UnknownIDs.Add(l);
                }
            }

            if (UnknownIDs.Count == 0)
            {
                return;
            }

            try
            {
                // UnknownIDs 去重
                UnknownIDs = UnknownIDs.Distinct().ToList();
                ESI.NET.EsiResponse<List<ESI.NET.Models.Universe.ResolvedInfo>> esra = await ESIClient.Universe.Names(UnknownIDs);
                if (ESIHelpers.ValidateESICall<List<ESI.NET.Models.Universe.ResolvedInfo>>(esra))
                {
                    foreach (ESI.NET.Models.Universe.ResolvedInfo ri in esra.Data)
                    {
                        if (ri.Category == ResolvedInfoCategory.Alliance)
                        {
                            ESI.NET.EsiResponse<ESI.NET.Models.Alliance.Alliance> esraA = await ESIClient.Alliance.Information((int)ri.Id);

                            if (ESIHelpers.ValidateESICall<ESI.NET.Models.Alliance.Alliance>(esraA))
                            {
                                AllianceIDToTicker[ri.Id] = esraA.Data.Ticker;
                                AllianceIDToName[ri.Id] = esraA.Data.Name;
                            }
                            else
                            {
                                AllianceIDToTicker[ri.Id] = "???????????????";
                                AllianceIDToName[ri.Id] = "?????";
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Update the Character ID data for specified list
        /// 更新指定列表的角色ID数据
        /// </summary>
        public async Task ResolveCharacterIDs(List<int> IDs)
        {
            if (IDs.Count == 0)
            {
                return;
            }

            // strip out any ID's we already know..
            List<int> UnknownIDs = new List<int>();
            foreach (int l in IDs)
            {
                if (!CharacterIDToName.ContainsKey(l))
                {
                    UnknownIDs.Add(l);
                }
            }

            if (UnknownIDs.Count == 0)
            {
                return;
            }

            try
            {
                ESI.NET.EsiResponse<List<ESI.NET.Models.Universe.ResolvedInfo>> esra = await ESIClient.Universe.Names(UnknownIDs);
                if (ESIHelpers.ValidateESICall<List<ESI.NET.Models.Universe.ResolvedInfo>>(esra))
                {
                    foreach (ESI.NET.Models.Universe.ResolvedInfo ri in esra.Data)
                    {
                        if (ri.Category == ResolvedInfoCategory.Character)
                        {
                            CharacterIDToName[ri.Id] = ri.Name;
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Save the Data to disk
        /// 保存数据到磁盘
        /// </summary>
        public void SaveData()
        {
            // save off only the ESI authenticated Characters so create a new copy to serialise from..
            // 仅保存ESI认证的角色，因此创建一个新副本以序列化..
            List<LocalCharacter> saveList = new List<LocalCharacter>();

            foreach (LocalCharacter c in LocalCharacters)
            {
                if (!string.IsNullOrEmpty(c.ESIRefreshToken))
                {
                    saveList.Add(c);
                }
            }

            XmlSerializer xms = new XmlSerializer(typeof(List<LocalCharacter>));
            string dataFilename = Path.Combine(SaveDataRootFolder, "Characters_" + LocalCharacter.SaveVersion + ".dat");

            using (TextWriter tw = new StreamWriter(dataFilename))
            {
                xms.Serialize(tw, saveList);
            }

            string jbFileName = Path.Combine(SaveDataRootFolder, "JumpBridges_" + JumpBridge.SaveVersion + ".dat");
            Serialization.SerializeToDisk<List<JumpBridge>>(JumpBridges, jbFileName);

            List<string> beaconsToSave = new List<string>();
            foreach (System s in Systems)
            {
                if (s.HasJumpBeacon)
                {
                    beaconsToSave.Add(s.Name);
                }
            }

            // save the intel channels / intel filters
            // 保存情报频道/情报过滤器
            File.WriteAllLines(Path.Combine(SaveDataRootFolder, "IntelChannels.txt"), IntelFilters);
            File.WriteAllLines(Path.Combine(SaveDataRootFolder, "IntelClearFilters.txt"), IntelClearFilters);
            File.WriteAllLines(Path.Combine(SaveDataRootFolder, "IntelIgnoreFilters.txt"), IntelIgnoreFilters);
            File.WriteAllLines(Path.Combine(SaveDataRootFolder, "IntelAlertFilters.txt"), IntelAlertFilters);
            File.WriteAllLines(Path.Combine(SaveDataRootFolder, "CynoBeacons.txt"), beaconsToSave);
        }

        /// <summary>
        /// Setup the intel watcher;  Loads the intel channel filter list and creates the file system watchers
        /// 设置情报观察者; 加载情报频道过滤器列表并创建文件系统观察者
        /// </summary>
        public void SetupIntelWatcher()
        {
            IntelDataList = new FixedQueue<IntelData>();
            IntelDataList.SetSizeLimit(250);

            IntelFilters = new List<string>();

            string intelFileFilter = Path.Combine(SaveDataRootFolder, "IntelChannels.txt");

            if (File.Exists(intelFileFilter))
            {
                StreamReader file = new StreamReader(intelFileFilter);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        IntelFilters.Add(line);
                    }
                }
            }
            else
            {
                IntelFilters.Add("Int");
            }

            IntelClearFilters = new List<string>();
            string intelClearFileFilter = Path.Combine(SaveDataRootFolder, "IntelClearFilters.txt");

            if (File.Exists(intelClearFileFilter))
            {
                StreamReader file = new StreamReader(intelClearFileFilter);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        IntelClearFilters.Add(line);
                    }
                }
            }
            else
            {
                // default
                IntelClearFilters.Add("Clr");
                IntelClearFilters.Add("Clear");
            }

            IntelIgnoreFilters = new List<string>();
            string intelIgnoreFileFilter = Path.Combine(SaveDataRootFolder, "IntelIgnoreFilters.txt");

            if (File.Exists(intelIgnoreFileFilter))
            {
                StreamReader file = new StreamReader(intelIgnoreFileFilter);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        IntelIgnoreFilters.Add(line);
                    }
                }
            }
            else
            {
                // default
                IntelIgnoreFilters.Add("Status");
            }

            IntelAlertFilters = new List<string>();
            string intelAlertFileFilter = Path.Combine(SaveDataRootFolder, "IntelAlertFilters.txt");

            if (File.Exists(intelAlertFileFilter))
            {
                StreamReader file = new StreamReader(intelAlertFileFilter);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        IntelAlertFilters.Add(line);
                    }
                }
            }
            else
            {
                // default, alert on nothing
                IntelAlertFilters.Add("");
            }

            intelFileReadPos = new Dictionary<string, int>();

            if (string.IsNullOrEmpty(EVELogFolder) || !Directory.Exists(EVELogFolder))
            {
                string[] logFolderLoc = { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "Logs" }; 
                EVELogFolder =  Path.Combine(logFolderLoc);
            }

            string chatlogFolder = Path.Combine(EVELogFolder, "Chatlogs");

            if (Directory.Exists(chatlogFolder))
            {
                intelFileWatcher = new FileSystemWatcher(chatlogFolder)
                {
                    Filter = "*.txt",
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                intelFileWatcher.Changed += IntelFileWatcher_Changed;
            }
        }

        /// <summary>
        /// Setup the game log0 watcher
        /// 设置游戏日志观察者
        /// </summary>
        public void SetupGameLogWatcher()
        {
            gameFileReadPos = new Dictionary<string, int>();
            gamelogFileCharacterMap = new Dictionary<string, string>();

            GameLogList = new FixedQueue<GameLogData>();
            GameLogList.SetSizeLimit(50);

            if (string.IsNullOrEmpty(EVELogFolder) || !Directory.Exists(EVELogFolder))
            {
                string[] logFolderLoc = { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "Logs" };
                EVELogFolder = Path.Combine(logFolderLoc);
            }

            string gameLogFolder = Path.Combine(EVELogFolder, "Gamelogs") ;

            if (Directory.Exists(gameLogFolder))
            {
                gameLogFileWatcher = new FileSystemWatcher(gameLogFolder)
                {
                    Filter = "*.txt",
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                gameLogFileWatcher.Changed += GameLogFileWatcher_Changed;
            }
        }

        public void SetupLogFileTriggers()
        {
            // -----------------------------------------------------------------
            // SUPER HACK WARNING....
            //
            // Start up a thread which just reads the text files in the eve log folder
            // by opening and closing them it updates the sytem meta files which
            // causes the file watcher to operate correctly otherwise this data
            // doesnt get updated until something other than the eve client reads these files

            List<string> logFolders = new List<string>();
            string chatLogFolder = Path.Combine(EVELogFolder, "Chatlogs");
            string gameLogFolder = Path.Combine(EVELogFolder, "Gamelogs");

            logFolders.Add(chatLogFolder);
            logFolders.Add(gameLogFolder);

            new Thread(() =>
            {
                LogFileCacheTrigger(logFolders);
            }).Start();

            // END SUPERHACK
            // -----------------------------------------------------------------
        }

        private void LogFileCacheTrigger(List<string> eveLogFolders)
        {
            Thread.CurrentThread.IsBackground = false;

            foreach (string dir in eveLogFolders)
            {
                if (!Directory.Exists(dir))
                {
                    return;
                }
            }

            // loop forever
            while (WatcherThreadShouldTerminate == false)
            {
                foreach (string folder in eveLogFolders)
                {
                    DirectoryInfo di = new DirectoryInfo(folder);
                    FileInfo[] files = di.GetFiles("*.txt");
                    foreach (FileInfo file in files)
                    {
                        bool readFile = false;
                        foreach (string intelFilterStr in IntelFilters)
                        {
                            if (file.Name.Contains(intelFilterStr, StringComparison.OrdinalIgnoreCase))
                            {
                                readFile = true;
                                break;
                            }
                        }

                        // local files
                        if (file.Name.Contains("Local_"))
                        {
                            readFile = true;
                        }

                        // gamelogs
                        if (folder.Contains("Gamelogs"))
                        {
                            readFile = true;
                        }

                        // only read files from the last day
                        if (file.CreationTime > DateTime.Now.AddDays(-1) && readFile)
                        {
                            FileStream ifs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            ifs.Seek(0, SeekOrigin.End);
                            ifs.Close();
                        }
                    }

                    Thread.Sleep(1500);
                }
            }
        }

        public void ShuddownIntelWatcher()
        {
            if (intelFileWatcher != null)
            {
                intelFileWatcher.Changed -= IntelFileWatcher_Changed;
            }
            WatcherThreadShouldTerminate = true;
        }

        public void ShuddownGameLogWatcher()
        {
            if (gameLogFileWatcher != null)
            {
                gameLogFileWatcher.Changed -= GameLogFileWatcher_Changed;
            }
            WatcherThreadShouldTerminate = true;
        }

        public void ShutDown()
        {
            ShuddownIntelWatcher();
            ShuddownGameLogWatcher();
            BackgroundThreadShouldTerminate = true;

            /*ZKillFeed.ShutDown();*/
        }

        /// <summary>
        /// Update The Universe Data from the various ESI end points
        /// 更新各种ESI端点的宇宙数据
        /// </summary>
        public void UpdateESIUniverseData()
        {
            UpdateKillsFromESI();
            UpdateJumpsFromESI();
            UpdateSOVFromESI();
            UpdateIncursionsFromESI();

            UpdateSovStructureUpdate();

            // TEMP Disabled
            //();
        }

        /// <summary>
        /// Update the Alliance and Ticker data for all SOV owners in the specified region
        /// 更新指定区域所有SOV所有者的联盟和标记数据
        /// </summary>
        public void UpdateIDsForMapRegion(string name)
        {
            MapRegion r = GetRegion(name);
            if (r == null)
            {
                return;
            }

            List<int> IDToResolve = new List<int>();

            foreach (KeyValuePair<string, MapSystem> kvp in r.MapSystems)
            {
                if (kvp.Value.ActualSystem.SOVAllianceID != 0 && !AllianceIDToName.ContainsKey(kvp.Value.ActualSystem.SOVAllianceID) && !IDToResolve.Contains(kvp.Value.ActualSystem.SOVAllianceID))
                {
                    IDToResolve.Add(kvp.Value.ActualSystem.SOVAllianceID);
                }
            }

            _ = ResolveAllianceIDs(IDToResolve);
        }

        /// <summary>
        /// Update the current Thera Connections from EVE-Scout
        /// 更新当前的Thera连接
        /// </summary>
        public async void UpdateTheraConnections()
        {
            // 欧服专属 国服没有
            /*string theraApiURL = "https://api.eve-scout.com/v2/public/signatures?system_name=Thera";
            string strContent = string.Empty;

            try
            {
                HttpClient hc = new HttpClient();
                var response = await hc.GetAsync(theraApiURL);
                response.EnsureSuccessStatusCode();
                strContent = await response.Content.ReadAsStringAsync();

                JsonTextReader jsr = new JsonTextReader(new StringReader(strContent));

                TheraConnections.Clear();

                /*
                    new format
                    
                    "id": "46",
                    "created_at": "2023-12-02T11:24:49.000Z",
                    "created_by_id": 93027866,
                    "created_by_name": "Das d'Alembert",
                    "updated_at": "2023-12-02T11:27:01.000Z",
                    "updated_by_id": 93027866,
                    "updated_by_name": "Das d'Alembert",
                    "completed_at": "2023-12-02T11:27:01.000Z",
                    "completed_by_id": 93027866,
                    "completed_by_name": "Das d'Alembert",
                    "completed": true,
                    "wh_exits_outward": true,
                    "wh_type": "Q063",
                    "max_ship_size": "medium",
                    "expires_at": "2023-12-03T04:24:49.000Z",
                    "remaining_hours": 14,
                    "signature_type": "wormhole",
                    "out_system_id": 31000005,
                    "out_system_name": "Thera",
                    "out_signature": "HMM-222",
                    "in_system_id": 30001715,
                    "in_system_class": "hs",
                    "in_system_name": "Moutid",
                    "in_region_id": 10000020,
                    "in_region_name": "Tash-Murkon",
                    "in_signature": "LPI-677"
                 #1#

                while (jsr.Read())
                {
                    if (jsr.TokenType == JsonToken.StartObject)
                    {
                        JObject obj = JObject.Load(jsr);
                        string inSignatureId = obj["in_signature"].ToString();
                        string outSignatureId = obj["out_signature"].ToString();
                        long solarSystemId = long.Parse(obj["in_system_id"].ToString());
                        string wormHoleEOL = obj["expires_at"].ToString();
                        string type = obj["signature_type"].ToString();

                        if (type != null && type == "wormhole" && solarSystemId != 0 && wormHoleEOL != null && SystemIDToName.ContainsKey(solarSystemId))
                        {
                            System theraConnectionSystem = GetEveSystemFromID(solarSystemId);

                            TheraConnection tc = new TheraConnection(theraConnectionSystem.Name, theraConnectionSystem.Region, inSignatureId, outSignatureId, wormHoleEOL);
                            TheraConnections.Add(tc);
                        }
                    }
                }
            }
            catch
            {
                return;
            }

            if (TheraUpdateEvent != null)
            {
                TheraUpdateEvent();
            }*/
        }

        public void UpdateMetaliminalStorms()
        {
            MetaliminalStorms.Clear();

            List<Storm> ls = Storm.GetStorms();
            foreach (Storm s in ls)
            {
                System sys = GetEveSystem(s.System);
                if (sys != null)
                {
                    MetaliminalStorms.Add(s);
                }
            }

            // now update the Strong and weak areas around the storm
            // 现在更新风暴周围的强弱区域
            foreach (Storm s in MetaliminalStorms)
            {
                // The Strong area is 1 jump out from the centre
                // 强区域距中心1跳
                List<string> strongArea = Navigation.GetSystemsXJumpsFrom(new List<string>(), s.System, 1);

                // The weak area is 3 jumps out from the centre
                // 弱区域距中心3跳
                List<string> weakArea = Navigation.GetSystemsXJumpsFrom(new List<string>(), s.System, 3);

                // strip the strong area out of the weak so we dont have overlapping icons
                // 剥离弱区域，以免有重叠的图标
                s.WeakArea = weakArea.Except(strongArea).ToList();

                // strip the centre out of the strong area
                // 剥离中心，以免有重叠的图标
                strongArea.Remove(s.Name);

                s.StrongArea = strongArea;
            }
            if (StormsUpdateEvent != null)
            {
                StormsUpdateEvent();
            }
        }

        public async void UpdateFactionWarfareInfo()
        {
            FactionWarfareSystems.Clear();

            try
            {
                ESI.NET.EsiResponse<List<ESI.NET.Models.FactionWarfare.FactionWarfareSystem>> esr = await ESIClient.FactionWarfare.Systems();

                string debugListofSytems = "";

                if (ESIHelpers.ValidateESICall<List<ESI.NET.Models.FactionWarfare.FactionWarfareSystem>>(esr))
                {
                    foreach (ESI.NET.Models.FactionWarfare.FactionWarfareSystem i in esr.Data)
                    {
                        FactionWarfareSystemInfo fwsi = new FactionWarfareSystemInfo();
                        fwsi.SystemState = FactionWarfareSystemInfo.State.None;

                        fwsi.OccupierID = i.OccupierFactionId;
                        fwsi.OccupierName = FactionWarfareSystemInfo.OwnerIDToName(i.OccupierFactionId);

                        fwsi.OwnerID = i.OwnerFactionId;
                        fwsi.OwnerName = FactionWarfareSystemInfo.OwnerIDToName(i.OwnerFactionId);

                        fwsi.SystemID = i.SolarSystemId;
                        fwsi.SystemName = GetEveSystemNameFromID(i.SolarSystemId);
                        fwsi.LinkSystemID = 0;
                        fwsi.VictoryPoints = i.VictoryPoints;
                        fwsi.VictoryPointsThreshold = i.VictoryPointsThreshold;

                        FactionWarfareSystems.Add(fwsi);

                        debugListofSytems += fwsi.SystemName + "\n";
                    }
                }

                // step 1, identify all the Frontline systems, these will be systems with connections to other systems with a different occupier
                // 步骤1，识别所有前线系统，这些系统将与其他占领者不同的系统连接
                foreach (FactionWarfareSystemInfo fws in FactionWarfareSystems)
                {
                    System s = GetEveSystemFromID(fws.SystemID);
                    foreach (string js in s.Jumps)
                    {
                        foreach (FactionWarfareSystemInfo fwss in FactionWarfareSystems)
                        {
                            if (fwss.SystemName == js && fwss.OccupierID != fws.OccupierID)
                            {
                                fwss.SystemState = FactionWarfareSystemInfo.State.Frontline;
                                fws.SystemState = FactionWarfareSystemInfo.State.Frontline;
                            }
                        }
                    }
                }

                // step 2, itendify all commandline operations by flooding out one from the frontlines
                // 步骤2，通过从前线中排除一个来识别所有命令行操作
                foreach (FactionWarfareSystemInfo fws in FactionWarfareSystems)
                {
                    if (fws.SystemState == FactionWarfareSystemInfo.State.Frontline)
                    {
                        System s = GetEveSystemFromID(fws.SystemID);

                        foreach (string js in s.Jumps)
                        {
                            foreach (FactionWarfareSystemInfo fwss in FactionWarfareSystems)
                            {
                                if (fwss.SystemName == js && fwss.SystemState == FactionWarfareSystemInfo.State.None && fwss.OccupierID == fws.OccupierID)
                                {
                                    fwss.SystemState = FactionWarfareSystemInfo.State.CommandLineOperation;
                                    fwss.LinkSystemID = fws.SystemID;
                                }
                            }
                        }
                    }
                }

                // step 3, itendify all Rearguard operations by flooding out one from the command lines
                // 步骤3，通过从命令行中排除一个来识别所有后卫操作
                foreach (FactionWarfareSystemInfo fws in FactionWarfareSystems)
                {
                    if (fws.SystemState == FactionWarfareSystemInfo.State.CommandLineOperation)
                    {
                        System s = GetEveSystemFromID(fws.SystemID);

                        foreach (string js in s.Jumps)
                        {
                            foreach (FactionWarfareSystemInfo fwss in FactionWarfareSystems)
                            {
                                if (fwss.SystemName == js && fwss.SystemState == FactionWarfareSystemInfo.State.None && fwss.OccupierID == fws.OccupierID)
                                {
                                    fwss.SystemState = FactionWarfareSystemInfo.State.Rearguard;
                                    fwss.LinkSystemID = fws.SystemID;
                                }
                            }
                        }
                    }
                }

                // for ease remove all "none" systems
                //FactionWarfareSystems.RemoveAll(sys => sys.SystemState == FactionWarfareSystemInfo.State.None);
            }
            catch { }
        }

        public void AddUpdateJumpBridge(string from, string to, long stationID)
        {
            // validate
            // 验证
            if (GetEveSystem(from) == null || GetEveSystem(to) == null)
            {
                return;
            }

            bool found = false;

            foreach (JumpBridge jb in JumpBridges)
            {
                if (jb.From == from)
                {
                    found = true;
                    jb.FromID = stationID;
                }
                if (jb.To == from)
                {
                    found = true;
                    jb.ToID = stationID;
                }
            }

            if (!found)
            {
                JumpBridge njb = new JumpBridge(from, to);
                njb.FromID = stationID;
                JumpBridges.Add(njb);
            }
        }

        /// <summary>
        /// Initialise the eve manager
        /// </summary>
        private void Init()
        {
            IOptions<EsiConfig> config = Options.Create(new EsiConfig()
            {
                EsiUrl = "https://ali-esi.evepc.163.com/",
                DataSource = DataSource.Serenity,
                ClientId = EveAppConfig.ClientID,
                SecretKey = "Unneeded",
                CallbackUrl = EveAppConfig.CallbackURL,
                UserAgent = "SMT-map-app",
            });

            ESIClient = new ESI.NET.EsiClient(config);
            ESIScopes = new List<string>
            {
                "esi-location.read_location.v1",
                "esi-search.search_structures.v1",
                "esi-clones.read_clones.v1",
                "esi-universe.read_structures.v1",
                "esi-fleets.read_fleet.v1",
                "esi-ui.write_waypoint.v1",
                "esi-characters.read_standings.v1",
                "esi-location.read_online.v1",
                "esi-characters.read_fatigue.v1",
                "esi-corporations.read_contacts.v1",
                "esi-alliances.read_contacts.v1",
                "esi-characters.read_contacts.v1"
            };

            foreach (MapRegion rr in Regions)
            {
                // link to the real systems
                foreach (KeyValuePair<string, MapSystem> kvp in rr.MapSystems)
                {
                    kvp.Value.ActualSystem = GetEveSystem(kvp.Value.Name);
                }
            }

            LoadCharacters();

            InitTheraConnections();

            InitMetaliminalStorms();
            InitFactionWarfareInfo();
            InitPOI();

            ActiveSovCampaigns = new List<SOVCampaign>();

            /*InitZKillFeed();*/

            // Removed as the api site is down
            //UpdateCoalitionInfo();

            StartBackgroundThread();
        }

        private void InitPOI()
        {
            PointsOfInterest = new List<POI>();

            try
            {
                string POIcsv = Path.Combine(DataRootFolder, "POI.csv");
                if (File.Exists(POIcsv))
                {
                    StreamReader file = new StreamReader(POIcsv);

                    string line;
                    line = file.ReadLine();
                    while ((line = file.ReadLine()) != null)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }
                        string[] bits = line.Split(',');

                        if (bits.Length < 4)
                        {
                            continue;
                        }

                        string system = bits[0];
                        string type = bits[1];
                        string desc = bits[2];
                        string longdesc = bits[3];

                        if (GetEveSystem(system) == null)
                        {
                            continue;
                        }

                        POI p = new POI() { System = system, Type = type, ShortDesc = desc, LongDesc = longdesc };

                        PointsOfInterest.Add(p);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Initialise the Thera Connection Data from EVE-Scout
        /// 从EVE-Scout初始化Thera连接数据
        /// </summary>
        private void InitTheraConnections()
        {
            TheraConnections = new List<TheraConnection>();
            UpdateTheraConnections();
        }

        /// <summary>
        /// Initialise the Zarzakh Connection Data
        /// 初始化Zarzakh连接数据
        /// </summary>
        private void InitZarzakhConnections()
        {
            List<string> zcon = new List<string>();
            foreach (System s in Systems)
            {
                if (s.HasJoveGate)
                {
                    zcon.Add(s.Name);
                }
            }

            Navigation.UpdateZarzakhConnections(zcon);
        }


        private void InitMetaliminalStorms()
        {
            MetaliminalStorms = new List<Storm>();
        }

        private void InitFactionWarfareInfo()
        {
            FactionWarfareSystems = new List<FactionWarfareSystemInfo>();
            UpdateFactionWarfareInfo();
        }

        /// <summary>
        /// Initialise the ZKillBoard Feed
        /// 初始化KB
        /// </summary>
        private void InitZKillFeed()
        {
            ZKillFeed = new ZKillRedisQ();
            ZKillFeed.VerString = VersionStr;
            ZKillFeed.Initialise();
        }

        /// <summary>
        /// Intel File watcher changed handler
        /// 信息文件改变处理程序
        /// </summary>
        private void IntelFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            string changedFile = e.FullPath;

            string[] channelParts = e.Name.Split("_");
            string channelName = string.Join("_", channelParts, 0, channelParts.Length - 3);

            bool processFile = false;
            bool localChat = false;

            // check if the changed file path contains the name of a channel we're looking for
            // 检查更改的文件路径是否包含我们正在查找的频道名称
            foreach (string intelFilterStr in IntelFilters)
            {
                if (changedFile.Contains(intelFilterStr, StringComparison.OrdinalIgnoreCase))
                {
                    processFile = true;
                    break;
                }
            }

            if (changedFile.Contains("本地_"))
            {
                localChat = true;
                processFile = true;
            }

            if (processFile)
            {
                try
                {
                    Encoding fe = Misc.GetEncoding(changedFile);
                    FileStream ifs = new FileStream(changedFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    StreamReader file = new StreamReader(ifs, fe);

                    int fileReadFrom = 0;

                    // have we seen this file before
                    if (intelFileReadPos.ContainsKey(changedFile))
                    {
                        fileReadFrom = intelFileReadPos[changedFile];
                    }
                    else
                    {
                        if (localChat)
                        {
                            string system = string.Empty;
                            string characterName = string.Empty;

                            // read the iniital block
                            while (!file.EndOfStream)
                            {
                                string l = file.ReadLine();
                                fileReadFrom++;

                                // explicitly skip just "local"
                                if (l.Contains("Channel Name:    本地"))
                                {
                                    // now can read the next line
                                    l = file.ReadLine(); // should be the "Listener : <CharName>"
                                    fileReadFrom++;

                                    characterName = l.Split(':')[1].Trim();

                                    bool addChar = true;
                                    foreach (EVEData.LocalCharacter c in LocalCharacters)
                                    {
                                        if (characterName == c.Name)
                                        {
                                            c.Location = system;
                                            c.LocalChatFile = changedFile;

                                            System s = GetEveSystem(system);
                                            if (s != null)
                                            {
                                                c.Region = s.Region;
                                            }
                                            else
                                            {
                                                c.Region = "";
                                            }

                                            addChar = false;
                                        }
                                    }

                                    if (addChar)
                                    {
                                        LocalCharacters.Add(new EVEData.LocalCharacter(characterName, changedFile, system));
                                        if (LocalCharacterUpdateEvent != null)
                                        {
                                            LocalCharacterUpdateEvent();
                                        }
                                    }

                                    break;
                                }
                            }
                        }

                        while (file.ReadLine() != null)
                        {
                            fileReadFrom++;
                        }

                        fileReadFrom--;
                        file.BaseStream.Seek(0, SeekOrigin.Begin);
                    }

                    for (int i = 0; i < fileReadFrom; i++)
                    {
                        file.ReadLine();
                    }

                    string line = file.ReadLine();

                    while (line != null)
                    {                    
                        // trim any items off the front
                        if (line.Contains('[') && line.Contains(']'))
                        {
                            line = line.Substring(line.IndexOf("["));
                        }

                        if (line == "")
                        {
                            line = file.ReadLine();
                            continue;
                        }

                        fileReadFrom++;

                        if (localChat)
                        {
                            if (line.StartsWith("[") && line.Contains("EVE系统 > 频道更换为本地"))
                            {
                                string system = line.Split(':').Last().Trim();

                                foreach (EVEData.LocalCharacter c in LocalCharacters)
                                {
                                    if (c.LocalChatFile == changedFile)
                                    {
                                        c.Location = system;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // check if it is in the intel list already (ie if you have multiple clients running)
                            // 检查它是否已在情报列表中（即如果您有多个客户端正在运行）
                            bool addToIntel = true;

                            int start = line.IndexOf('>') + 1;
                            string newIntelString = line.Substring(start);

                            if (newIntelString != null)
                            {
                                foreach (EVEData.IntelData idl in IntelDataList)
                                {
                                    if (idl.IntelString == newIntelString && (DateTime.Now - idl.IntelTime).Seconds < 5)
                                    {
                                        addToIntel = false;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                addToIntel = false;
                            }
                            // TODO - 暂时不知道国服怎么显示
                            if (line.Contains("Channel MOTD:"))
                            {
                                addToIntel = false;
                            }

                            foreach (String ignoreMarker in IntelIgnoreFilters)
                            {
                                if (line.IndexOf(ignoreMarker, StringComparison.OrdinalIgnoreCase) != -1)
                                {
                                    addToIntel = false;
                                    break;
                                }
                            }


                            if (addToIntel)
                            {
                                EVEData.IntelData id = new EVEData.IntelData(line, channelName);


                                foreach (string s in id.IntelString.Split(' '))
                                {
                                    if (s == "" || s.Length < 3)
                                    {
                                        continue;
                                    }

                                    foreach (String clearMarker in IntelClearFilters)
                                    {
                                        if (clearMarker.IndexOf(s, StringComparison.OrdinalIgnoreCase) == 0)
                                        {
                                            id.ClearNotification = true;
                                        }
                                    }

                                    foreach (System sys in Systems)
                                    {
                                        if (sys.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) == 0 || s.IndexOf(sys.Name, StringComparison.OrdinalIgnoreCase) == 0)
                                        {
                                            id.Systems.Add(sys.Name);
                                        }
                                    }
                                }

                                IntelDataList.Enqueue(id);

                                if (IntelUpdatedEvent != null)
                                {
                                    IntelUpdatedEvent(IntelDataList);
                                }

                            }
                        }

                        line = file.ReadLine();
                    }

                    ifs.Close();

                    intelFileReadPos[changedFile] = fileReadFrom;
                }
                catch
                {
                }
            }
            else
            {
            }
        }

        /// <summary>
        /// GameLog File watcher changed handler
        /// 日志文件观察者更改处理程序
        /// </summary>
        private void GameLogFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            string changedFile = e.FullPath;
            string characterName = string.Empty;

            try
            {
                Encoding fe = EVEDataUtils.Misc.GetEncoding(changedFile);
                FileStream ifs = new FileStream(changedFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                StreamReader file = new StreamReader(ifs, fe);

                int fileReadFrom = 0;

                // have we seen this file before
                if (gameFileReadPos.ContainsKey(changedFile))
                {
                    fileReadFrom = gameFileReadPos[changedFile];
                }
                else
                {
                    // read the iniital block
                    while (!file.EndOfStream)
                    {
                        string l = file.ReadLine();
                        fileReadFrom++;

                        // explicitly skip just "local"
                        if (l.Contains("游戏记录"))
                        {
                            // now can read the next line
                            l = file.ReadLine(); // should be the "Listener : <CharName>"

                            // something wrong with the log file; clear
                            if (!l.Contains("收听者"))
                            {
                                if (gameFileReadPos.ContainsKey(changedFile))
                                {
                                    gameFileReadPos.Remove(changedFile);
                                }

                                return;
                            }

                            fileReadFrom++;

                            gamelogFileCharacterMap[changedFile] = l.Split(':')[1].Trim();

                            // session started
                            l = file.ReadLine();
                            fileReadFrom++;

                            // header end
                            l = file.ReadLine();
                            fileReadFrom++;

                            // as its new; skip the entire file -1
                            break;
                        }
                    }

                    while (!file.EndOfStream)
                    {
                        string l = file.ReadLine();
                        fileReadFrom++;
                    }

                    // back one line
                    fileReadFrom--;

                    file.BaseStream.Seek(0, SeekOrigin.Begin);
                }

                characterName = gamelogFileCharacterMap[changedFile];

                for (int i = 0; i < fileReadFrom; i++)
                {
                    file.ReadLine();
                }

                string line = file.ReadLine();

                while (line != null)
                {                    // trim any items off the front
                    if (line == "" || !line.StartsWith("["))
                    {
                        line = file.ReadLine();
                        fileReadFrom++;
                        continue;
                    }

                    fileReadFrom++;

                    int typeStartPos = line.IndexOf("(") + 1;
                    int typeEndPos = line.IndexOf(")");

                    // file corrupt
                    if (typeStartPos < 1 || typeEndPos < 1)
                    {
                        continue;
                    }

                    string type = line.Substring(typeStartPos, typeEndPos - typeStartPos);

                    line = line.Substring(typeEndPos + 1);

                    // strip the formatting from the log
                    line = Regex.Replace(line, "<.*?>", String.Empty);

                    GameLogData gd = new GameLogData()
                    {
                        Character = characterName,
                        Text = line,
                        Severity = type,
                        Time = DateTime.Now,
                    };

                    GameLogList.Enqueue(gd);
                    if (GameLogAddedEvent != null)
                    {
                        GameLogAddedEvent(GameLogList);
                    }

                    foreach (LocalCharacter lc in LocalCharacters)
                    {
                        if (lc.Name == characterName)
                        {
                            if (type == "combat")
                            {
                                if (CombatEvent != null)
                                {
                                    lc.GameLogWarningText = line;
                                    CombatEvent(characterName, line);
                                }
                            }
                            if (line.Contains("移动式观测站的脉冲波影响，你的隐形状态已解除。") || line.Contains("你的隐形状态已解除"))
                            {
                                if (ShipDecloakedEvent != null)
                                {
                                    ShipDecloakedEvent(characterName, line);
                                    lc.GameLogWarningText = line;
                                }
                            }
                        }
                    }

                    line = file.ReadLine();
                    gameFileReadPos[changedFile] = fileReadFrom;
                }

                ifs.Close();

                gameFileReadPos[changedFile] = fileReadFrom;
            }
            catch
            {
            }
        }

        /// <summary>
        /// Load the character data from disk
        /// 加载磁盘上的角色数据
        /// </summary>
        private void LoadCharacters()
        {

            string dataFilename = Path.Combine(SaveDataRootFolder,  "Characters_" + LocalCharacter.SaveVersion + ".dat");
            if (!File.Exists(dataFilename))
            {
                return;
            }

            try
            {
                List<LocalCharacter> loadList;
                XmlSerializer xms = new XmlSerializer(typeof(List<LocalCharacter>));

                FileStream fs = new FileStream(dataFilename, FileMode.Open, FileAccess.Read);
                XmlReader xmlr = XmlReader.Create(fs);

                loadList = (List<LocalCharacter>)xms.Deserialize(xmlr);

                foreach (LocalCharacter c in loadList)
                {
                    c.ESIAccessToken = string.Empty;
                    c.ESIAccessTokenExpiry = DateTime.MinValue;
                    c.LocalChatFile = string.Empty;
                    c.Location = string.Empty;
                    c.Region = string.Empty;

                    LocalCharacters.Add(c);

                    if (LocalCharacterUpdateEvent != null)
                    {
                        LocalCharacterUpdateEvent();
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Start the Low Frequency Update Thread
        /// 开始低频更新线程
        /// </summary>
        private void StartBackgroundThread()
        {
            new Thread(async () =>
            {
                Thread.CurrentThread.IsBackground = false;



                // loop forever
                while (BackgroundThreadShouldTerminate == false)
                {
                    // character Update
                    if ((NextCharacterUpdate - DateTime.Now).Ticks < 0)
                    {
                        NextCharacterUpdate = DateTime.Now + CharacterUpdateRate;

                        for (int i = 0; i < LocalCharacters.Count; i++)
                        {
                            LocalCharacter c = LocalCharacters.ElementAt(i);
                            await c.Update();
                        }
                    }

                    // sov update
                    if ((NextSOVCampaignUpdate - DateTime.Now).Ticks < 0)
                    {
                        NextSOVCampaignUpdate = DateTime.Now + SOVCampaignUpdateRate;
                        UpdateSovCampaigns();
                    }

                    // low frequency update
                    if ((NextLowFreqUpdate - DateTime.Now).Minutes < 0)
                    {
                        NextLowFreqUpdate = DateTime.Now + LowFreqUpdateRate;

                        UpdateESIUniverseData();
                        UpdateServerInfo();
                        UpdateTheraConnections();
                    }

                    if ((NextDotlanUpdate - DateTime.Now).Minutes < 0)
                    {
                        UpdateDotlanKillDeltaInfo();
                    }

                    Thread.Sleep(100);
                }
            }).Start();
        }

        private async void UpdateDotlanKillDeltaInfo()
        {
            // set the update for 20 minutes from now initially which will be pushed further once we have the last-modified
            // however if the request fails we still push out the request..
            // 设置更新为20分钟后，最初将被推迟，一旦我们有了最后修改的时间 但是如果请求失败，我们仍然推出请求
            NextDotlanUpdate = DateTime.Now + TimeSpan.FromMinutes(20);
            // TODO 国服暂时没有这个接口


            /*try
            {
                string dotlanNPCDeltaAPIurl = "https://evemaps.dotlan.net/ajax/npcdelta";

                HttpClient hc = new HttpClient();
                string versionNum = VersionStr.Split("_")[1];
                string userAgent = $"Mozilla/5.0 (Slazanger's Map Tool https://github.com/Slazanger/SMT/ version {versionNum} )";
                hc.DefaultRequestHeaders.Add("User-Agent", userAgent);
                hc.DefaultRequestHeaders.IfModifiedSince = LastDotlanUpdate;

                // set the etag if we have one
                if (LastDotlanETAG != "")
                {
                    hc.DefaultRequestHeaders.IfNoneMatch.Add(new EntityTagHeaderValue(LastDotlanETAG));
                }


                var response = await hc.GetAsync(dotlanNPCDeltaAPIurl);


                // update the next request to the last modified + 1hr + random offset
                if (response.Content.Headers.LastModified.HasValue)
                {
                    Random rndUpdateOffset = new Random();
                    NextDotlanUpdate = response.Content.Headers.LastModified.Value.DateTime.ToLocalTime() + TimeSpan.FromMinutes(60) + TimeSpan.FromSeconds(rndUpdateOffset.Next(1, 300));
                }

                // update the values for the next request;
                LastDotlanUpdate = DateTime.Now;
                if (response.Headers.ETag != null)
                {
                    LastDotlanETAG = response.Headers.ETag.Tag;
                }



                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    // we shouldn't hit this; the first request should update the request beyond the last-modified expiring
                    // LastDotlanUpdate = DateTime.Now;
                }
                else
                {
                    // read the data
                    string strContent = string.Empty;
                    strContent = await response.Content.ReadAsStringAsync();


                    // parse the json response into kvp string/strings (system id)/(delta)
                    Dictionary<string, string> killdDeltadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(strContent);

                    foreach (var kvp in killdDeltadata)
                    {
                        Console.WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}");
                        int systemId = int.Parse(kvp.Key);
                        int killDelta = int.Parse(kvp.Value);

                        System s = GetEveSystemFromID(systemId);
                        if (s != null)
                        {
                            s.NPCKillsDeltaLastHour = killDelta;
                        }
                    }
                }
            }
            catch (Exception e)
            {
            }*/
        }

        /// <summary>
        /// Start the ESI download for the Jump info
        /// 开始从ESI下载跳跃信息
        /// </summary>
        private async void UpdateIncursionsFromESI()
        {
            try
            {
                ESI.NET.EsiResponse<List<ESI.NET.Models.Incursions.Incursion>> esr = await ESIClient.Incursions.All();
                if (ESIHelpers.ValidateESICall<List<ESI.NET.Models.Incursions.Incursion>>(esr))
                {
                    foreach (ESI.NET.Models.Incursions.Incursion i in esr.Data)
                    {
                        foreach (long s in i.InfestedSystems)
                        {
                            EVEData.System sys = GetEveSystemFromID(s);
                            if (sys != null)
                            {
                                sys.ActiveIncursion = true;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Start the ESI download for the Jump info
        /// </summary>
        private async void UpdateJumpsFromESI()
        {
            try
            {
                ESI.NET.EsiResponse<List<ESI.NET.Models.Universe.Jumps>> esr = await ESIClient.Universe.Jumps();
                if (ESIHelpers.ValidateESICall<List<ESI.NET.Models.Universe.Jumps>>(esr))
                {
                    foreach (ESI.NET.Models.Universe.Jumps j in esr.Data)
                    {
                        EVEData.System es = GetEveSystemFromID(j.SystemId);
                        if (es != null)
                        {
                            es.JumpsLastHour = j.ShipJumps;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Start the ESI download for the kill info
        /// </summary>
        private async void UpdateKillsFromESI()
        {
            try
            {
                ESI.NET.EsiResponse<List<ESI.NET.Models.Universe.Kills>> esr = await ESIClient.Universe.Kills();
                if (ESIHelpers.ValidateESICall<List<ESI.NET.Models.Universe.Kills>>(esr))
                {
                    foreach (ESI.NET.Models.Universe.Kills k in esr.Data)
                    {
                        EVEData.System es = GetEveSystemFromID(k.SystemId);
                        if (es != null)
                        {
                            es.NPCKillsLastHour = k.NpcKills;
                            es.PodKillsLastHour = k.PodKills;
                            es.ShipKillsLastHour = k.ShipKills;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private async void UpdateSovCampaigns()
        {
            try
            {
                bool sendUpdateEvent = false;

                foreach (SOVCampaign sc in ActiveSovCampaigns)
                {
                    sc.Valid = false;
                }

                List<int> allianceIDsToResolve = new List<int>();

                ESI.NET.EsiResponse<List<ESI.NET.Models.Sovereignty.Campaign>> esr = await ESIClient.Sovereignty.Campaigns();
                if (ESIHelpers.ValidateESICall<List<ESI.NET.Models.Sovereignty.Campaign>>(esr))
                {
                    foreach (ESI.NET.Models.Sovereignty.Campaign c in esr.Data)
                    {
                        SOVCampaign ss = null;

                        foreach (SOVCampaign asc in ActiveSovCampaigns)
                        {
                            if (asc.CampaignID == c.CampaignId)
                            {
                                ss = asc;
                            }
                        }

                        if (ss == null)
                        {
                            System sys = GetEveSystemFromID(c.SolarSystemId);
                            if (sys == null)
                            {
                                continue;
                            }

                            ss = new SOVCampaign
                            {
                                CampaignID = c.CampaignId,
                                DefendingAllianceID = c.DefenderId,
                                System = sys.Name,
                                Region = sys.Region,
                                StartTime = c.StartTime,
                                DefendingAllianceName = "",
                            };

                            if (c.EventType == "ihub_defense")
                            {
                                ss.Type = "IHub";
                            }

                            if (c.EventType == "tcu_defense")
                            {
                                ss.Type = "TCU";
                            }

                            ActiveSovCampaigns.Add(ss);
                            sendUpdateEvent = true;
                        }

                        if (ss.AttackersScore != c.AttackersScore || ss.DefendersScore != c.DefenderScore)
                        {
                            sendUpdateEvent = true;
                        }

                        ss.AttackersScore = c.AttackersScore;
                        ss.DefendersScore = c.DefenderScore;
                        ss.Valid = true;

                        if (AllianceIDToName.ContainsKey(ss.DefendingAllianceID))
                        {
                            ss.DefendingAllianceName = AllianceIDToName[ss.DefendingAllianceID];
                        }
                        else
                        {
                            if (!allianceIDsToResolve.Contains(ss.DefendingAllianceID))
                            {
                                allianceIDsToResolve.Add(ss.DefendingAllianceID);
                            }
                        }

                        int NodesToWin = (int)Math.Ceiling(ss.DefendersScore / 0.07);
                        int NodesToDefend = (int)Math.Ceiling(ss.AttackersScore / 0.07);
                        ss.State = $"节点剩余\n进攻方 : {NodesToWin}\n防守方 : {NodesToDefend}";

                        ss.TimeToStart = ss.StartTime - DateTime.UtcNow;

                        if (ss.StartTime < DateTime.UtcNow)
                        {
                            ss.IsActive = true;
                        }
                        else
                        {
                            ss.IsActive = false;
                        }
                    }
                }

                if (allianceIDsToResolve.Count > 0)
                {
                    await ResolveAllianceIDs(allianceIDsToResolve);
                }

                foreach (SOVCampaign sc in ActiveSovCampaigns.ToList())
                {
                    if (string.IsNullOrEmpty(sc.DefendingAllianceName) && AllianceIDToName.ContainsKey(sc.DefendingAllianceID))
                    {
                        sc.DefendingAllianceName = AllianceIDToName[sc.DefendingAllianceID];
                    }

                    if (sc.Valid == false)
                    {
                        ActiveSovCampaigns.Remove(sc);
                        sendUpdateEvent = true;
                    }
                }

                if (sendUpdateEvent)
                {
                    if (SovUpdateEvent != null)
                    {
                        SovUpdateEvent();
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Start the ESI download for the kill info
        /// </summary>
        private async void UpdateSOVFromESI()
        {
            string url = @"https://ali-esi.evepc.163.com/v1/sovereignty/map/?datasource=serenity";
            string strContent = string.Empty;

            try
            {
                HttpClient hc = new HttpClient();
                var response = await hc.GetAsync(url);
                response.EnsureSuccessStatusCode();
                strContent = await response.Content.ReadAsStringAsync();
                JsonTextReader jsr = new JsonTextReader(new StringReader(strContent));

                // JSON feed is now in the format : [{ "system_id": 30035042,  and then optionally alliance_id, corporation_id and corporation_id, faction_id },
                while (jsr.Read())
                {
                    if (jsr.TokenType == JsonToken.StartObject)
                    {
                        JObject obj = JObject.Load(jsr);
                        long systemID = long.Parse(obj["system_id"].ToString());

                        if (SystemIDToName.ContainsKey(systemID))
                        {
                            System es = GetEveSystem(SystemIDToName[systemID]);
                            if (es != null)
                            {
                                if (obj["alliance_id"] != null)
                                {
                                    es.SOVAllianceID = int.Parse(obj["alliance_id"].ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private async void UpdateSovStructureUpdate()
        {
            try
            {
                ESI.NET.EsiResponse<List<ESI.NET.Models.Sovereignty.Structure>> esr = await ESIClient.Sovereignty.Structures();
                if (ESIHelpers.ValidateESICall<List<ESI.NET.Models.Sovereignty.Structure>>(esr))
                {
                    foreach (ESI.NET.Models.Sovereignty.Structure ss in esr.Data)
                    {
                        EVEData.System es = GetEveSystemFromID(ss.SolarSystemId);
                        if (es != null)
                        {
                            // structures : 
                            // Old TCU  : 32226
                            // Old iHub :  32458

                            es.SOVAllianceID = ss.AllianceId;

                            if (ss.TypeId == 32226)
                            {
                                es.TCUVunerabliltyStart = ss.VulnerableStartTime;
                                es.TCUVunerabliltyEnd = ss.VulnerableEndTime;
                                es.TCUOccupancyLevel = (float)ss.VulnerabilityOccupancyLevel;
                            }

                            if (ss.TypeId == 32458)
                            {
                                es.IHubVunerabliltyStart = ss.VulnerableStartTime;
                                es.IHubVunerabliltyEnd = ss.VulnerableEndTime;
                                es.IHubOccupancyLevel = (float)ss.VulnerabilityOccupancyLevel;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Start the download for the Server Info
        /// </summary>
        private async void UpdateServerInfo()
        {
            try
            {
                ESI.NET.EsiResponse<ESI.NET.Models.Status.Status> esr = await ESIClient.Status.Retrieve();

                if (ESIHelpers.ValidateESICall<ESI.NET.Models.Status.Status>(esr))
                {
                    ServerInfo.Name = "Serenity";
                    ServerInfo.NumPlayers = esr.Data.Players;
                    ServerInfo.ServerVersion = esr.Data.ServerVersion.ToString();
                }
                else
                {
                    ServerInfo.Name = "Serenity";
                    ServerInfo.NumPlayers = 0;
                    ServerInfo.ServerVersion = "";
                }
            }
            catch { }
        }

        public void RemoveCharacter(LocalCharacter lc)
        {
            LocalCharacters.Remove(lc);

            if (LocalCharacterUpdateEvent != null)
            {
                LocalCharacterUpdateEvent();
            }
        }

        public string CreateAuthenticationUrl(List<string> scope = null, string state = null)
        {
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(72, 3);
            interpolatedStringHandler.AppendLiteral("https://");
            interpolatedStringHandler.AppendFormatted("login.evepc.163.com");
            interpolatedStringHandler.AppendLiteral("/v2/oauth/authorize/?response_type=code&redirect_uri=");
            interpolatedStringHandler.AppendFormatted(Uri.EscapeDataString(EveAppConfig.CallbackURL));
            interpolatedStringHandler.AppendLiteral("&client_id=");
            interpolatedStringHandler.AppendFormatted(EveAppConfig.ClientID);
            string authenticationUrl = interpolatedStringHandler.ToStringAndClear();
            if (scope != null)
                authenticationUrl = authenticationUrl + "&scope=" + string.Join("%20", (IEnumerable<string>) scope.Distinct<string>().ToList<string>());
            if (state != null)
            {
                authenticationUrl = authenticationUrl + "&state=" + state;
                authenticationUrl = authenticationUrl + "&device_id=" + state;
            }
            return authenticationUrl;
        }
        
        public static async Task<SsoToken> GetToken(GrantType grantType, string code)
        {
            string body = "grant_type=" + grantType.ToEsiValue();
            switch (grantType)
            {
              case GrantType.AuthorizationCode:
                body = body + "&code=" + code;
                body = body + "&client_id=" + EveAppConfig.ClientID;
                break;
              case GrantType.RefreshToken:
                body = body + "&refresh_token=" + Uri.EscapeDataString(code);
                body = body + "&client_id=" + EveAppConfig.ClientID;
                break;
            }
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://login.evepc.163.com/v2/oauth/token")
            {
              Content = (HttpContent) new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            
            HttpResponseMessage response = await new HttpClient().SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
              string error = JsonConvert.DeserializeAnonymousType(content, new
              {
                error_description = string.Empty
              }).error_description;
              throw new ArgumentException(error);
            }
            SsoToken token = JsonConvert.DeserializeObject<SsoToken>(content);
            SsoToken token1 = token;
            return token1;
        }
        
        public static async Task<AuthorizedCharacterData> Verify(SsoToken token)
        {
            AuthorizedCharacterData authorizedCharacter = new AuthorizedCharacterData();
            try
            {
                // 向https://ali-esi.evepc.163.com/verify?token= 发送get请求
                HttpResponseMessage response = await new HttpClient().GetAsync("https://ali-esi.evepc.163.com/verify?token=" + token.AccessToken);
                string content = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string error = JsonConvert.DeserializeAnonymousType(content, new
                    {
                        error_description = string.Empty
                    }).error_description;
                    throw new ArgumentException(error);
                }
                authorizedCharacter = JsonConvert.DeserializeObject<AuthorizedCharacterData>(content);
                
                string url = "https://ali-esi.evepc.163.com/latest/characters/affiliation/?datasource=serenity";
                StringContent body = new StringContent(JsonConvert.SerializeObject((object) new int[1]
                {
                    authorizedCharacter.CharacterID
                }), Encoding.UTF8, "application/json");
                HttpClient client = new HttpClient();
                HttpResponseMessage characterResponse = await client.PostAsync(url, (HttpContent) body).ConfigureAwait(false);
                if (characterResponse.StatusCode == HttpStatusCode.OK)
                {
                    EsiResponse<List<Affiliation>> affiliations = new EsiResponse<List<Affiliation>>(characterResponse, "Post|/character/affiliations/");
                    Affiliation characterData = affiliations.Data.First<Affiliation>();
                    authorizedCharacter.AllianceID = characterData.AllianceId;
                    authorizedCharacter.CorporationID = characterData.CorporationId;
                    authorizedCharacter.FactionID = characterData.FactionId;
                    affiliations = (EsiResponse<List<Affiliation>>) null;
                    characterData = (Affiliation) null;
                }
            }
            catch (Exception e)
            {
                
            }
           
            
            AuthorizedCharacterData authorizedCharacterData = authorizedCharacter;
            authorizedCharacter = (AuthorizedCharacterData) null;
            return authorizedCharacterData;

        }
    }
}