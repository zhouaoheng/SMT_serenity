// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//
//    using StructureHunter;
//
//    var structures = Structures.FromJson(jsonString);

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace StructureHunter
{
    public enum RegionName
    { AR00001, AR00002, AR00003, 艾里迪亚, BR00004, BR00005, BR00006, BR00007, BR00008, 暗涌之域, 血脉, CR00009, CR00010, CR00011, CR00012, CR00013, CR00014, CR00015, 地窖, 卡彻, 云环, 钴蓝边域, 柯尔斯, DR00016, DR00017, DR00018, DR00019, DR00020, DR00021, DR00022, DR00023, 德克廉, 绝地之域, 德里克, 底特里德, 破碎, 多美, ER00024, ER00025, ER00026, ER00027, ER00028, ER00029, 埃索特亚, 精华之域, 琉蓝之穹, 埃维希尔, FR00030, 斐德, 非塔波利斯, 源泉之域, 对舞之域, 吉勒西斯, 大荒野, 西玛特尔, 伊梅瑟亚, 绝径, 因斯姆尔, 卡多尔, 卡尼迪, 柯埃佐, 长征, 糟粕之域, 美特伯里斯, 摩登赫斯, 欧莎, 欧米斯特, 域外走廊, 外环, 摄魂之域, 贝斯, 佩利根弗, 宁静之域, 普罗维登斯, 黑渊, 逑瑞斯, 灼热之径, 金纳泽, 孤独之域, 混浊, 辛迪加, 塔什蒙贡, 特纳, 特里菲斯, 幽暗之域, 赛塔德洱, 伏尔戈, 卡勒瓦拉阔地, 螺旋之域, 特布特, 静寂谷, 维纳尔, 维格温铎, 邪恶湾流 };

    public enum TypeName
    { 空堡, 阿塔诺, 阿兹贝尔, 铁壁, 星城, 莱塔卢, 索迪约, 塔塔拉 };

    public static class Serialize
    {
        public static string ToJson(this Dictionary<string, Structures> self) => JsonConvert.SerializeObject(self, StructureHunter.Converter.Settings);
    }

    public partial class Location
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }
    }

    public partial class Structures
    {
        [JsonProperty("firstSeen")]
        public DateTimeOffset FirstSeen { get; set; }

        [JsonProperty("lastSeen")]
        public DateTimeOffset LastSeen { get; set; }

        [JsonProperty("location")]
        public Location Location { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("public")]
        public bool Public { get; set; }

        [JsonProperty("regionId")]
        public long RegionId { get; set; }

        [JsonProperty("regionName")]
        public RegionName RegionName { get; set; }

        [JsonProperty("systemId")]
        public long SystemId { get; set; }

        [JsonProperty("systemName")]
        public string SystemName { get; set; }

        [JsonProperty("typeId")]
        public long? TypeId { get; set; }

        [JsonProperty("typeName")]
        public TypeName? TypeName { get; set; }
    }

    public partial class Structures
    {
        public static Dictionary<string, Structures> FromJson(string json) => JsonConvert.DeserializeObject<Dictionary<string, Structures>>(json, StructureHunter.Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                RegionNameConverter.Singleton,
                TypeNameConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class RegionNameConverter : JsonConverter
    {
        public static readonly RegionNameConverter Singleton = new RegionNameConverter();

        public override bool CanConvert(Type t) => t == typeof(RegionName) || t == typeof(RegionName?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "A-R00001":
                    return RegionName.AR00001;

                case "A-R00002":
                    return RegionName.AR00002;

                case "A-R00003":
                    return RegionName.AR00003;

                case "艾里迪亚":
                    return RegionName.艾里迪亚;

                case "B-R00004":
                    return RegionName.BR00004;

                case "B-R00005":
                    return RegionName.BR00005;

                case "B-R00006":
                    return RegionName.BR00006;

                case "B-R00007":
                    return RegionName.BR00007;

                case "B-R00008":
                    return RegionName.BR00008;

                case "暗涌之域":
                    return RegionName.暗涌之域;

                case "血脉":
                    return RegionName.血脉;

                case "C-R00009":
                    return RegionName.CR00009;

                case "C-R00010":
                    return RegionName.CR00010;

                case "C-R00011":
                    return RegionName.CR00011;

                case "C-R00012":
                    return RegionName.CR00012;

                case "C-R00013":
                    return RegionName.CR00013;

                case "C-R00014":
                    return RegionName.CR00014;

                case "C-R00015":
                    return RegionName.CR00015;

                case "地窖":
                    return RegionName.地窖;

                case "卡彻":
                    return RegionName.卡彻;

                case "云环":
                    return RegionName.云环;

                case "钴蓝边域":
                    return RegionName.钴蓝边域;

                case "柯尔斯":
                    return RegionName.柯尔斯;

                case "D-R00016":
                    return RegionName.DR00016;

                case "D-R00017":
                    return RegionName.DR00017;

                case "D-R00018":
                    return RegionName.DR00018;

                case "D-R00019":
                    return RegionName.DR00019;

                case "D-R00020":
                    return RegionName.DR00020;

                case "D-R00021":
                    return RegionName.DR00021;

                case "D-R00022":
                    return RegionName.DR00022;

                case "D-R00023":
                    return RegionName.DR00023;

                case "德克廉":
                    return RegionName.德克廉;

                case "绝地之域":
                    return RegionName.绝地之域;

                case "德里克":
                    return RegionName.德里克;

                case "底特里德":
                    return RegionName.底特里德;

                case "破碎":
                    return RegionName.破碎;

                case "多美":
                    return RegionName.多美;

                case "E-R00024":
                    return RegionName.ER00024;

                case "E-R00025":
                    return RegionName.ER00025;

                case "E-R00026":
                    return RegionName.ER00026;

                case "E-R00027":
                    return RegionName.ER00027;

                case "E-R00028":
                    return RegionName.ER00028;

                case "E-R00029":
                    return RegionName.ER00029;

                case "埃索特亚":
                    return RegionName.埃索特亚;

                case "精华之域":
                    return RegionName.精华之域;

                case "琉蓝之穹":
                    return RegionName.琉蓝之穹;

                case "埃维希尔":
                    return RegionName.埃维希尔;

                case "F-R00030":
                    return RegionName.FR00030;

                case "斐德":
                    return RegionName.斐德;

                case "非塔波利斯":
                    return RegionName.非塔波利斯;

                case "源泉之域":
                    return RegionName.源泉之域;

                case "对舞之域":
                    return RegionName.对舞之域;

                case "吉勒西斯":
                    return RegionName.吉勒西斯;

                case "大荒野":
                    return RegionName.大荒野;

                case "西玛特尔":
                    return RegionName.西玛特尔;

                case "伊梅瑟亚":
                    return RegionName.伊梅瑟亚;

                case "绝径":
                    return RegionName.绝径;

                case "因斯姆尔":
                    return RegionName.因斯姆尔;

                case "卡多尔":
                    return RegionName.卡多尔;

                case "卡尼迪":
                    return RegionName.卡尼迪;

                case "柯埃佐":
                    return RegionName.柯埃佐;

                case "长征":
                    return RegionName.长征;

                case "糟粕之域":
                    return RegionName.糟粕之域;

                case "美特伯里斯":
                    return RegionName.美特伯里斯;

                case "摩登赫斯":
                    return RegionName.摩登赫斯;

                case "欧莎":
                    return RegionName.欧莎;

                case "欧米斯特":
                    return RegionName.欧米斯特;

                case "域外走廊":
                    return RegionName.域外走廊;

                case "外环":
                    return RegionName.外环;

                case "摄魂之域":
                    return RegionName.摄魂之域;

                case "贝斯":
                    return RegionName.贝斯;

                case "佩利根弗":
                    return RegionName.佩利根弗;

                case "宁静之域":
                    return RegionName.宁静之域;

                case "普罗维登斯":
                    return RegionName.普罗维登斯;

                case "黑渊":
                    return RegionName.黑渊;

                case "逑瑞斯":
                    return RegionName.逑瑞斯;

                case "灼热之径":
                    return RegionName.灼热之径;

                case "金纳泽":
                    return RegionName.金纳泽;

                case "孤独之域":
                    return RegionName.孤独之域;

                case "混浊":
                    return RegionName.混浊;

                case "辛迪加":
                    return RegionName.辛迪加;

                case "塔什蒙贡":
                    return RegionName.塔什蒙贡;

                case "特纳":
                    return RegionName.特纳;

                case "特里菲斯":
                    return RegionName.特里菲斯;

                case "幽暗之域":
                    return RegionName.幽暗之域;

                case "赛塔德洱":
                    return RegionName.赛塔德洱;

                case "伏尔戈":
                    return RegionName.伏尔戈;

                case "卡勒瓦拉阔地":
                    return RegionName.卡勒瓦拉阔地;

                case "螺旋之域":
                    return RegionName.螺旋之域;

                case "特布特":
                    return RegionName.特布特;

                case "静寂谷":
                    return RegionName.静寂谷;

                case "维纳尔":
                    return RegionName.维纳尔;

                case "维格温铎":
                    return RegionName.维格温铎;

                case "邪恶湾流":
                    return RegionName.邪恶湾流;
            }
            throw new Exception("Cannot unmarshal type RegionName");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (RegionName)untypedValue;
            switch (value)
            {
                case RegionName.AR00001:
                    serializer.Serialize(writer, "A-R00001");
                    return;

                case RegionName.AR00002:
                    serializer.Serialize(writer, "A-R00002");
                    return;

                case RegionName.AR00003:
                    serializer.Serialize(writer, "A-R00003");
                    return;

                case RegionName.艾里迪亚:
                    serializer.Serialize(writer, "艾里迪亚");
                    return;

                case RegionName.BR00004:
                    serializer.Serialize(writer, "B-R00004");
                    return;

                case RegionName.BR00005:
                    serializer.Serialize(writer, "B-R00005");
                    return;

                case RegionName.BR00006:
                    serializer.Serialize(writer, "B-R00006");
                    return;

                case RegionName.BR00007:
                    serializer.Serialize(writer, "B-R00007");
                    return;

                case RegionName.BR00008:
                    serializer.Serialize(writer, "B-R00008");
                    return;

                case RegionName.暗涌之域:
                    serializer.Serialize(writer, "暗涌之域");
                    return;

                case RegionName.血脉:
                    serializer.Serialize(writer, "血脉");
                    return;

                case RegionName.CR00009:
                    serializer.Serialize(writer, "C-R00009");
                    return;

                case RegionName.CR00010:
                    serializer.Serialize(writer, "C-R00010");
                    return;

                case RegionName.CR00011:
                    serializer.Serialize(writer, "C-R00011");
                    return;

                case RegionName.CR00012:
                    serializer.Serialize(writer, "C-R00012");
                    return;

                case RegionName.CR00013:
                    serializer.Serialize(writer, "C-R00013");
                    return;

                case RegionName.CR00014:
                    serializer.Serialize(writer, "C-R00014");
                    return;

                case RegionName.CR00015:
                    serializer.Serialize(writer, "C-R00015");
                    return;

                case RegionName.地窖:
                    serializer.Serialize(writer, "地窖");
                    return;

                case RegionName.卡彻:
                    serializer.Serialize(writer, "卡彻");
                    return;

                case RegionName.云环:
                    serializer.Serialize(writer, "云环");
                    return;

                case RegionName.钴蓝边域:
                    serializer.Serialize(writer, "钴蓝边域");
                    return;

                case RegionName.柯尔斯:
                    serializer.Serialize(writer, "柯尔斯");
                    return;

                case RegionName.DR00016:
                    serializer.Serialize(writer, "D-R00016");
                    return;

                case RegionName.DR00017:
                    serializer.Serialize(writer, "D-R00017");
                    return;

                case RegionName.DR00018:
                    serializer.Serialize(writer, "D-R00018");
                    return;

                case RegionName.DR00019:
                    serializer.Serialize(writer, "D-R00019");
                    return;

                case RegionName.DR00020:
                    serializer.Serialize(writer, "D-R00020");
                    return;

                case RegionName.DR00021:
                    serializer.Serialize(writer, "D-R00021");
                    return;

                case RegionName.DR00022:
                    serializer.Serialize(writer, "D-R00022");
                    return;

                case RegionName.DR00023:
                    serializer.Serialize(writer, "D-R00023");
                    return;

                case RegionName.德克廉:
                    serializer.Serialize(writer, "德克廉");
                    return;

                case RegionName.绝地之域:
                    serializer.Serialize(writer, "绝地之域");
                    return;

                case RegionName.德里克:
                    serializer.Serialize(writer, "德里克");
                    return;

                case RegionName.底特里德:
                    serializer.Serialize(writer, "底特里德");
                    return;

                case RegionName.破碎:
                    serializer.Serialize(writer, "破碎");
                    return;

                case RegionName.多美:
                    serializer.Serialize(writer, "多美");
                    return;

                case RegionName.ER00024:
                    serializer.Serialize(writer, "E-R00024");
                    return;

                case RegionName.ER00025:
                    serializer.Serialize(writer, "E-R00025");
                    return;

                case RegionName.ER00026:
                    serializer.Serialize(writer, "E-R00026");
                    return;

                case RegionName.ER00027:
                    serializer.Serialize(writer, "E-R00027");
                    return;

                case RegionName.ER00028:
                    serializer.Serialize(writer, "E-R00028");
                    return;

                case RegionName.ER00029:
                    serializer.Serialize(writer, "E-R00029");
                    return;

                case RegionName.埃索特亚:
                    serializer.Serialize(writer, "埃索特亚");
                    return;

                case RegionName.精华之域:
                    serializer.Serialize(writer, "精华之域");
                    return;

                case RegionName.琉蓝之穹:
                    serializer.Serialize(writer, "琉蓝之穹");
                    return;

                case RegionName.埃维希尔:
                    serializer.Serialize(writer, "埃维希尔");
                    return;

                case RegionName.FR00030:
                    serializer.Serialize(writer, "F-R00030");
                    return;

                case RegionName.斐德:
                    serializer.Serialize(writer, "斐德");
                    return;

                case RegionName.非塔波利斯:
                    serializer.Serialize(writer, "非塔波利斯");
                    return;

                case RegionName.源泉之域:
                    serializer.Serialize(writer, "源泉之域");
                    return;

                case RegionName.对舞之域:
                    serializer.Serialize(writer, "对舞之域");
                    return;

                case RegionName.吉勒西斯:
                    serializer.Serialize(writer, "吉勒西斯");
                    return;

                case RegionName.大荒野:
                    serializer.Serialize(writer, "大荒野");
                    return;

                case RegionName.西玛特尔:
                    serializer.Serialize(writer, "西玛特尔");
                    return;

                case RegionName.伊梅瑟亚:
                    serializer.Serialize(writer, "伊梅瑟亚");
                    return;

                case RegionName.绝径:
                    serializer.Serialize(writer, "绝径");
                    return;

                case RegionName.因斯姆尔:
                    serializer.Serialize(writer, "因斯姆尔");
                    return;

                case RegionName.卡多尔:
                    serializer.Serialize(writer, "卡多尔");
                    return;

                case RegionName.卡尼迪:
                    serializer.Serialize(writer, "卡尼迪");
                    return;

                case RegionName.柯埃佐:
                    serializer.Serialize(writer, "柯埃佐");
                    return;

                case RegionName.长征:
                    serializer.Serialize(writer, "长征");
                    return;

                case RegionName.糟粕之域:
                    serializer.Serialize(writer, "糟粕之域");
                    return;

                case RegionName.美特伯里斯:
                    serializer.Serialize(writer, "美特伯里斯");
                    return;

                case RegionName.摩登赫斯:
                    serializer.Serialize(writer, "摩登赫斯");
                    return;

                case RegionName.欧莎:
                    serializer.Serialize(writer, "欧莎");
                    return;

                case RegionName.欧米斯特:
                    serializer.Serialize(writer, "欧米斯特");
                    return;

                case RegionName.域外走廊:
                    serializer.Serialize(writer, "域外走廊");
                    return;

                case RegionName.外环:
                    serializer.Serialize(writer, "外环");
                    return;

                case RegionName.摄魂之域:
                    serializer.Serialize(writer, "摄魂之域");
                    return;

                case RegionName.贝斯:
                    serializer.Serialize(writer, "贝斯");
                    return;

                case RegionName.佩利根弗:
                    serializer.Serialize(writer, "佩利根弗");
                    return;

                case RegionName.宁静之域:
                    serializer.Serialize(writer, "宁静之域");
                    return;

                case RegionName.普罗维登斯:
                    serializer.Serialize(writer, "普罗维登斯");
                    return;

                case RegionName.黑渊:
                    serializer.Serialize(writer, "黑渊");
                    return;

                case RegionName.逑瑞斯:
                    serializer.Serialize(writer, "逑瑞斯");
                    return;

                case RegionName.灼热之径:
                    serializer.Serialize(writer, "灼热之径");
                    return;

                case RegionName.金纳泽:
                    serializer.Serialize(writer, "金纳泽");
                    return;

                case RegionName.孤独之域:
                    serializer.Serialize(writer, "孤独之域");
                    return;

                case RegionName.混浊:
                    serializer.Serialize(writer, "混浊");
                    return;

                case RegionName.辛迪加:
                    serializer.Serialize(writer, "辛迪加");
                    return;

                case RegionName.塔什蒙贡:
                    serializer.Serialize(writer, "塔什蒙贡");
                    return;

                case RegionName.特纳:
                    serializer.Serialize(writer, "特纳");
                    return;

                case RegionName.特里菲斯:
                    serializer.Serialize(writer, "特里菲斯");
                    return;

                case RegionName.幽暗之域:
                    serializer.Serialize(writer, "幽暗之域");
                    return;

                case RegionName.赛塔德洱:
                    serializer.Serialize(writer, "赛塔德洱");
                    return;

                case RegionName.伏尔戈:
                    serializer.Serialize(writer, "伏尔戈");
                    return;

                case RegionName.卡勒瓦拉阔地:
                    serializer.Serialize(writer, "卡勒瓦拉阔地");
                    return;

                case RegionName.螺旋之域:
                    serializer.Serialize(writer, "螺旋之域");
                    return;

                case RegionName.特布特:
                    serializer.Serialize(writer, "特布特");
                    return;

                case RegionName.静寂谷:
                    serializer.Serialize(writer, "静寂谷");
                    return;

                case RegionName.维纳尔:
                    serializer.Serialize(writer, "维纳尔");
                    return;

                case RegionName.维格温铎:
                    serializer.Serialize(writer, "维格温铎");
                    return;

                case RegionName.邪恶湾流:
                    serializer.Serialize(writer, "邪恶湾流");
                    return;
            }
            throw new Exception("Cannot marshal type RegionName");
        }
    }

    internal class TypeNameConverter : JsonConverter
    {
        public static readonly TypeNameConverter Singleton = new TypeNameConverter();

        public override bool CanConvert(Type t) => t == typeof(TypeName) || t == typeof(TypeName?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "空堡":
                    return TypeName.空堡;

                case "阿塔诺":
                    return TypeName.阿塔诺;

                case "阿兹贝尔":
                    return TypeName.阿兹贝尔;

                case "铁壁":
                    return TypeName.铁壁;

                case "星城":
                    return TypeName.星城;

                case "莱塔卢":
                    return TypeName.莱塔卢;

                case "索迪约":
                    return TypeName.索迪约;

                case "塔塔拉":
                    return TypeName.塔塔拉;
            }
            throw new Exception("Cannot unmarshal type TypeName");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (TypeName)untypedValue;
            switch (value)
            {
                case TypeName.空堡:
                    serializer.Serialize(writer, "空堡");
                    return;

                case TypeName.阿塔诺:
                    serializer.Serialize(writer, "阿塔诺");
                    return;

                case TypeName.阿兹贝尔:
                    serializer.Serialize(writer, "阿兹贝尔");
                    return;

                case TypeName.铁壁:
                    serializer.Serialize(writer, "铁壁");
                    return;

                case TypeName.星城:
                    serializer.Serialize(writer, "星城");
                    return;

                case TypeName.莱塔卢:
                    serializer.Serialize(writer, "莱塔卢");
                    return;

                case TypeName.索迪约:
                    serializer.Serialize(writer, "索迪约");
                    return;

                case TypeName.塔塔拉:
                    serializer.Serialize(writer, "塔塔拉");
                    return;
            }
            throw new Exception("Cannot marshal type TypeName");
        }
    }
}