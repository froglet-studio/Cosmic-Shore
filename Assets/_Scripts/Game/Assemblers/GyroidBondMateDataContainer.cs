using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public static class GyroidBondMateDataContainer
    {
        public static Dictionary<(GyroidBlockType, CornerSiteType), GyroidBondMateData> BondMateDataMap = InitializeBondMateData();

        private static Dictionary<(GyroidBlockType, CornerSiteType), GyroidBondMateData> InitializeBondMateData()
        {
            return new Dictionary<(GyroidBlockType, CornerSiteType), GyroidBondMateData>
            {
                // Example initialization, replace with actual data
                { (GyroidBlockType.AB, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(-1.3501522670729562f,2.338536121613463f,-0.0017254940332009916f),
                        DeltaUp = new Vector3(-0.8660245639508068f,-0.49999950953915356f,-0.0009366129620037023f),
                        DeltaForward = new Vector3(-0.0017704640275681045f,-0.0008529851738756212f,-1.931065308468478e-06f),
                        BlockType = GyroidBlockType.BA,
                        isTail = true
                    }
                },
                { (GyroidBlockType.AB, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(1.3342154086156648f,2.2550178687697615f,-0.11896293471226282f),
                        DeltaUp = new Vector3(0.8838682388731791f,-0.5389293303730315f,-0.07863558284711236f),
                        DeltaForward = new Vector3(-0.018228770519622878f,0.2054782795298074f,-0.021507720900732247f),
                        BlockType = GyroidBlockType.BC,
                        isTail = true
                    }
                },
                { (GyroidBlockType.AB, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(-1.3501540319990641f,-2.3385356118535574f,-0.0013283825792559378f),
                        DeltaUp = new Vector3(0.8660249881031888f,-0.5000002452305752f,0.0010222261940469601f),
                        DeltaForward = new Vector3(-0.0016234486769176504f,0.0011077786560099445f,-1.931381275731953e-06f),
                        BlockType = GyroidBlockType.BA,
                        isTail = false
                    }
                },
                { (GyroidBlockType.AB, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.BottomLeft,
                        DeltaPosition = new Vector3(1.2696511002855666f,-2.3663762924909366f,0.12192003072505742f),
                        DeltaUp = new Vector3(0.8410965126366061f,-1.534840905282246f,0.08076468402974513f),
                        DeltaForward = new Vector3(-0.17042303037525852f,-0.11743851720488901f,-0.02165254005768183f),
                        BlockType = GyroidBlockType.AF,
                        isTail = true
                    }
                },
                { (GyroidBlockType.BC, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(-1.4486491383571511f,2.2714657499875184f,-0.2070205745701088f),
                        DeltaUp = new Vector3(0.8778041177549085f,-1.4616980351863653f,0.12530706723444837f),
                        DeltaForward = new Vector3(-0.03500733635772444f,0.19566801387927443f,-0.01996126258248781f),
                        BlockType = GyroidBlockType.EsC,
                        isTail = false
                    }
                },
                { (GyroidBlockType.BC, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(1.4068331288014404f,2.300219698480685f,0.3488340024738964f),
                        DeltaUp = new Vector3(0.8524659549457894f,-0.5208785443804539f,0.20831441081517085f),
                        DeltaForward = new Vector3(-0.2079389390589778f,-0.06182197988261099f,-0.02381148099031219f),
                        BlockType = GyroidBlockType.CD,
                        isTail = true
                    }
                },
                { (GyroidBlockType.BC, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(-1.3043070521513271f,-2.343349420192177f,0.18081153828433583f),
                        DeltaUp = new Vector3(-0.8366181901439409f,-1.5348404994414264f,0.1159267100040684f),
                        DeltaForward = new Vector3(0.1899274695311566f,-0.08227654727894412f,-0.02165256145896511f),
                        BlockType = GyroidBlockType.BA,
                        isTail = true
                    }
                },
                { (GyroidBlockType.BC, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(1.3461239966929919f,-2.2283385290478153f,-0.3226253547587686f),
                        DeltaUp = new Vector3(-0.8634402977519717f,-0.5389308555388103f,0.2037945941376716f),
                        DeltaForward = new Vector3(0.1900187237013508f,-0.08031856136345236f,-0.02150767720490629f),
                        BlockType = GyroidBlockType.AB,
                        isTail = false
                    }
                },
                { (GyroidBlockType.CD, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(-1.2705883585527606f,2.3604030442768638f,-0.23150850926832556f),
                        DeltaUp = new Vector3(0.8625032416497994f,-1.4820266999053209f,0.1569211546991609f),
                        DeltaForward = new Vector3(-0.22413032254599405f,-0.0695077165354045f,-0.027926657878934953f),
                        BlockType = GyroidBlockType.EsD,
                        isTail = false
                    }
                },
                { (GyroidBlockType.CD, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(1.3011061971417486f,2.3867046621820704f,-0.2993661979183373f),
                        DeltaUp = new Vector3(0.8538414870759251f,-0.5167463839183296f,-0.19244044034096414f),
                        DeltaForward = new Vector3(0.230287223737534f,-0.0020211621326271297f,-0.026879606574104632f),
                        BlockType = GyroidBlockType.DE,
                        isTail = true
                    }
                },
                { (GyroidBlockType.CD, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(-1.3733061824464892f,-2.5642302595228976f,0.05287025655708644f),
                        DeltaUp = new Vector3(0.83215030470173f,-0.4462130918921978f,-0.028180152787784962f),
                        DeltaForward = new Vector3(-0.14225994444791668f,0.27150684603638464f,-0.048109168421714116f),
                        BlockType = GyroidBlockType.EsC,
                        isTail = false
                    }
                },
                { (GyroidBlockType.CD, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(1.321950076690403f,-2.373553823130297f,0.09409089092205214f),
                        DeltaUp = new Vector3(-0.8757415749647453f,-0.5208785443804539f,-0.05829538960852812f),
                        DeltaForward = new Vector3(0.04688967055756531f,0.21184100108925372f,-0.023811480990312245f),
                        BlockType = GyroidBlockType.BC,
                        isTail = false
                    }
                },
                { (GyroidBlockType.DE, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.BottomLeft,
                        DeltaPosition = new Vector3(-1.0362182692859037f,2.5349652289874633f,0.2883608933075501f),
                        DeltaUp = new Vector3(-0.7034085869299458f,-0.31361705773880805f,0.18255396111531175f),
                        DeltaForward = new Vector3(0.3788401487799432f,0.14977074566647697f,-0.08683358509094974f),
                        BlockType = GyroidBlockType.EG,
                        isTail = true
                    }
                },
                { (GyroidBlockType.DE, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(1.4546582062574323f,2.2643785294935976f,-0.2281870102707666f),
                        DeltaUp = new Vector3(0.881445469254506f,-0.5512658404598407f,-0.1507410745908317f),
                        DeltaForward = new Vector3(0.05684855235085173f,0.19528379152129058f,-0.02093017880697781f),
                        BlockType = GyroidBlockType.EF,
                        isTail = true
                    }
                },
                { (GyroidBlockType.DE, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(-1.1921539934690994f,-2.2216545059283344f,-0.5261422417714612f),
                        DeltaUp = new Vector3(0.8092612377439474f,-0.5262995633931191f,0.34396532977391275f),
                        DeltaForward = new Vector3(-0.15413372167621642f,-0.4203871482739467f,-0.1055259151543731f),
                        BlockType = GyroidBlockType.EsD,
                        isTail = false
                    }
                },
                { (GyroidBlockType.DE, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(1.4447376910860488f,-2.321344426647397f,-0.0033620682459561957f),
                        DeltaUp = new Vector3(-0.8754340212485114f,-0.5167476173139989f,-0.010434653306548192f),
                        DeltaForward = new Vector3(-0.112971488250796f,-0.20085239557319357f,-0.026879496654728927f),
                        BlockType = GyroidBlockType.CD,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EF, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.BottomLeft,
                        DeltaPosition = new Vector3(-1.3011410018203464f,2.5642270455462253f,-0.4502503556819665f),
                        DeltaUp = new Vector3(-0.7884217232780439f,-0.4462151987325816f,-0.2663307614899957f),
                        DeltaForward = new Vector3(-0.3046320363952545f,0.03335686948611513f,-0.04810922250529449f),
                        BlockType = GyroidBlockType.FG,
                        isTail = true
                    }
                },
                { (GyroidBlockType.EF, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(1.3049410445636045f,2.3472544832993036f,0.29241591612245477f),
                        DeltaUp = new Vector3(-0.8644750015759334f,-1.4616998987377106f,-0.19401766628521944f),
                        DeltaForward = new Vector3(-0.15561646589115807f,-0.1236566122900945f,-0.019961174142886944f),
                        BlockType = GyroidBlockType.AF,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EF, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(-1.3239476367349172f,-2.1500060892780204f,0.4163533427438294f),
                        DeltaUp = new Vector3(-0.8987258417733999f,-1.3392051493658679f,0.2822394615883882f),
                        DeltaForward = new Vector3(0.18045533078953305f,0.2730624521552089f,-0.05502950628519608f),
                        BlockType = GyroidBlockType.EG,
                        isTail = true
                    }
                },
                { (GyroidBlockType.EF, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(1.3261056177953774f,-2.3341036432903723f,-0.30209919630860993f),
                        DeltaUp = new Vector3(-0.870246207220392f,-0.5512658404598405f,0.20501663756084104f),
                        DeltaForward = new Vector3(0.14669474674498317f,-0.1410082285512812f,-0.020930178806977635f),
                        BlockType = GyroidBlockType.DE,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EG, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.BottomLeft,
                        DeltaPosition = new Vector3(-1.1879914810703731f,2.194976731681308f,0.6441802573190569f),
                        DeltaUp = new Vector3(-0.7796103430397499f,-0.5262991680623765f,0.40476337979769084f),
                        DeltaForward = new Vector3(0.2669973584627621f,-0.3595896849774196f,-0.10552604473194827f),
                        BlockType = GyroidBlockType.GEs,
                        isTail = true
                    }
                },
                { (GyroidBlockType.EG, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(1.440441383829151f,2.268634438830744f,-0.10442579578849275f),
                        DeltaUp = new Vector3(-0.8728305129869888f,-1.4820280351843196f,0.06229526442914696f),
                        DeltaForward = new Vector3(0.167967089671759f,-0.16413324445920546f,-0.027926723456755218f),
                        BlockType = GyroidBlockType.FG,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EG, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(-1.0843187302300552f,-2.519068107584366f,-0.25156978950878095f),
                        DeltaUp = new Vector3(0.7115754957508531f,-0.313617057738808f,0.14711566968538117f),
                        DeltaForward = new Vector3(-0.36532648677636287f,0.17989888513421595f,-0.08683358509094957f),
                        BlockType = GyroidBlockType.DE,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EG, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.BottomLeft,
                        DeltaPosition = new Vector3(1.485630758269093f,-2.032932122688842f,0.43181302389603454f),
                        DeltaUp = new Vector3(0.9002131876948173f,-1.3392051493658679f,0.26067453013456277f),
                        DeltaForward = new Vector3(-0.18420173625136282f,0.269851539567742f,-0.05502950628519611f),
                        BlockType = GyroidBlockType.EF,
                        isTail = true
                    }
                },
                { (GyroidBlockType.BA, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.BottomLeft,
                        DeltaPosition = new Vector3(-1.3501526348903923f,2.3385349746527426f,0.0017255461913739945f),
                        DeltaUp = new Vector3(-0.8660247290870524f,-0.4999997956417197f,0.0009366557439198786f),
                        DeltaForward = new Vector3(0.0017705979656557264f,0.0008531383525940074f,-1.931433121594951e-06f),
                        BlockType = GyroidBlockType.AB,
                        isTail = true
                    }
                },
                { (GyroidBlockType.BA, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(1.3342159064817878f,2.2550171928817773f,0.11896180621086216f),
                        DeltaUp = new Vector3(0.8838685108906169f,-0.5389297238510645f,0.07863483386306033f),
                        DeltaForward = new Vector3(0.01822929831444193f,-0.20547787491394764f,-0.021507645776681295f),
                        BlockType = GyroidBlockType.AF,
                        isTail = true
                    }
                },
                { (GyroidBlockType.BA, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(-1.3501529267077292f,-2.3385357392937056f,0.001329831577008056f),
                        DeltaUp = new Vector3(0.8660245623071634f,-0.4999995095391536f,-0.0010231465170158494f),
                        DeltaForward = new Vector3(0.001623938995529322f,-0.0011067743051439579f,-1.9310653085597676e-06f),
                        BlockType = GyroidBlockType.AB,
                        isTail = false
                    }
                },
                { (GyroidBlockType.BA, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(1.2696532342401206f,-2.3663758919635955f,-0.12191799336819709f),
                        DeltaUp = new Vector3(0.841096908739714f,-1.5348404994414264f,-0.08076323694875998f),
                        DeltaForward = new Vector3(0.1704221174705063f,0.11744002033425244f,-0.021652561458965054f),
                        BlockType = GyroidBlockType.BC,
                        isTail = true
                    }
                },
                { (GyroidBlockType.AF, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(-1.44864733271961f,2.2714664679402006f,0.20701983118243994f),
                        DeltaUp = new Vector3(0.8778031997348456f,-1.4616998987377108f,-0.12530664030553873f),
                        DeltaForward = new Vector3(0.03500664805068478f,-0.19566769430066377f,-0.01996117414288672f),
                        BlockType = GyroidBlockType.EF,
                        isTail = false
                    }
                },
                { (GyroidBlockType.AF, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(1.4068335655567856f,2.300218517039652f,-0.3488341882639199f),
                        DeltaUp = new Vector3(0.8524657531888495f,-0.5208781865246829f,-0.20831441518366028f),
                        DeltaForward = new Vector3(0.20793895367259657f,0.061822036131769564f,-0.023811487665945374f),
                        BlockType = GyroidBlockType.FG,
                        isTail = true
                    }
                },
                { (GyroidBlockType.AF, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(-1.3043078919574274f,-2.343346064207522f,-0.18081189243647386f),
                        DeltaUp = new Vector3(-0.8366186604303709f,-1.5348397172097579f,-0.11592692537438691f),
                        DeltaForward = new Vector3(-0.18992748596661113f,0.08227647335736436f,-0.02165255844010877f),
                        BlockType = GyroidBlockType.AB,
                        isTail = true
                    }
                },
                { (GyroidBlockType.AF, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(1.3461224634217834f,-2.22833773619564f,0.3226249003223116f),
                        DeltaUp = new Vector3(-0.8634397377538474f,-0.5389297238510644f,-0.20379441027667655f),
                        DeltaForward = new Vector3(-0.1900186728831069f,0.08031829850033137f,-0.02150764577668124f),
                        BlockType = GyroidBlockType.BA,
                        isTail = false
                    }
                },
                { (GyroidBlockType.FG, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(-1.270586180625124f,2.3604041305528263f,0.23150967021318503f),
                        DeltaUp = new Vector3(0.8625027107977543f,-1.482027353935253f,-0.1569221095848168f),
                        DeltaForward = new Vector3(0.2241311881829746f,0.06950696966292606f,-0.027926804083609168f),
                        BlockType = GyroidBlockType.EG,
                        isTail = false
                    }
                },
                { (GyroidBlockType.FG, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(1.3011074110726264f,2.3867042291827727f,0.2993664119977021f),
                        DeltaUp = new Vector3(0.8538420462721451f,-0.5167473949954642f,0.19244048599892724f),
                        DeltaForward = new Vector3(-0.23028745571258313f,0.00202160683157189f,-0.026879662421568598f),
                        BlockType = GyroidBlockType.GEs,
                        isTail = true
                    }
                },
                { (GyroidBlockType.FG, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(-1.3733082153110856f,-2.5642275021676584f,-0.05287044602020585f),
                        DeltaUp = new Vector3(0.8321517034508965f,-0.4462151987325814f,0.028180234519712327f),
                        DeltaForward = new Vector3(0.14225925811748663f,-0.27150739645639854f,-0.048109222505294036f),
                        BlockType = GyroidBlockType.EF,
                        isTail = false
                    }
                },
                { (GyroidBlockType.FG, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(1.3219478454313314f,-2.373554209965851f,-0.09409087975572217f),
                        DeltaUp = new Vector3(-0.875741375844854f,-0.5208781865246827f,0.05829542918081104f),
                        DeltaForward = new Vector3(-0.046889716544480466f,-0.2118410221346188f,-0.02381148766594514f),
                        BlockType = GyroidBlockType.AF,
                        isTail = false
                    }
                },
                { (GyroidBlockType.GEs, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(-1.0362174974554637f,2.5349646545988955f,-0.28836118244700226f),
                        DeltaUp = new Vector3(-0.7034080790934114f,-0.31361656898966644f,-0.1825540655616209f),
                        DeltaForward = new Vector3(-0.37884043870507333f,-0.14977038043112795f,-0.08683364608392752f),
                        BlockType = GyroidBlockType.EsD,
                        isTail = true
                    }
                },
                { (GyroidBlockType.GEs, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(1.4546574333682114f,2.264379440656472f,0.22818711272860892f),
                        DeltaUp = new Vector3(0.8814448240941786f,-0.5512646080621082f,0.15074119893626486f),
                        DeltaForward = new Vector3(-0.056848705142062694f,-0.19528325002505495f,-0.020930079687909234f),
                        BlockType = GyroidBlockType.EsC,
                        isTail = true
                    }
                },
                { (GyroidBlockType.GEs, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(-1.1921529647915954f,-2.221654110208135f,0.5261439400689134f),
                        DeltaUp = new Vector3(0.8092604653644528f,-0.526299012438284f,-0.3439663543773474f),
                        DeltaForward = new Vector3(0.1541342122613475f,0.42038793679498854f,-0.1055263659227721f),
                        BlockType = GyroidBlockType.EG,
                        isTail = false
                    }
                },
                { (GyroidBlockType.GEs, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.TopRight,
                       DeltaPosition = new Vector3(1.4447381517291034f,-2.321343885920763f,0.0033627836571326064f),
                        DeltaUp = new Vector3(-0.8754339012283855f,-0.5167473949954645f,0.010434309206040743f),
                        DeltaForward = new Vector3(0.11297151192634687f,0.20085318837339608f,-0.026879662421569098f),
                        BlockType = GyroidBlockType.FG,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EsC, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.BottomLeft,
                        DeltaPosition = new Vector3(-1.3011381463754175f,2.5642303535332482f,0.45024921980711796f),
                        DeltaUp = new Vector3(-0.7884204243976567f,-0.4462130918921984f,0.2663302401449727f),
                        DeltaForward = new Vector3(0.3046318794153856f,-0.03335675867919694f,-0.048109168421714185f),
                        BlockType = GyroidBlockType.CD,
                        isTail = true
                    }
                },
                { (GyroidBlockType.EsC, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(1.304944344817208f,2.347253019182199f,-0.2924168524853338f),
                        DeltaUp = new Vector3(-0.8644759253183194f,-1.461698035186366f,0.19401799826194538f),
                        DeltaForward = new Vector3(0.1556166735143277f,0.12365705161711928f,-0.019961262582487926f),
                        BlockType = GyroidBlockType.BC,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EsC, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.BottomRight,
                        DeltaPosition = new Vector3(-1.3239487596514636f,-2.1500035661690573f,-0.4163530440374141f),
                        DeltaUp = new Vector3(-0.8987266246047605f,-1.339203219643671f,-0.28223926568335944f),
                        DeltaForward = new Vector3(-0.1804556247362065f,-0.2730624187571273f,-0.05502955288667041f),
                        BlockType = GyroidBlockType.EsD,
                        isTail = true
                    }
                },
                { (GyroidBlockType.EsC, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.TopRight,
                        DeltaPosition = new Vector3(1.326103718279858f,-2.3341052818779358f,0.3020982063837049f),
                        DeltaUp = new Vector3(-0.8702456866737279f,-0.5512646080621085f,-0.20501613603334945f),
                        DeltaForward = new Vector3(-0.14669400458579718f,0.14100831292797036f,-0.020930079687909206f),
                        BlockType = GyroidBlockType.GEs,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EsD, CornerSiteType.TopLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.TopLeft,
                        Bondee = CornerSiteType.BottomLeft,
                        DeltaPosition = new Vector3(-1.187990593131417f,2.194978436436966f,-0.6441800102743214f),
                        DeltaUp = new Vector3(-0.7796101939256468f,-0.5262990249783199f,-0.4047635234996182f),
                        DeltaForward = new Vector3(-0.2669978059947522f,0.3595891038351812f,-0.10552594744773475f),
                        BlockType = GyroidBlockType.DE,
                        isTail = true
                    }
                },
                { (GyroidBlockType.EsD, CornerSiteType.TopRight), new GyroidBondMateData
                {
                        Substrate = CornerSiteType.TopRight,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(1.440442021429853f,2.2686338912587454f,0.1044277005533698f),
                        DeltaUp = new Vector3(-0.872831154032023f,-1.4820266999053209f,-0.06229645466308387f),
                        DeltaForward = new Vector3(-0.16796751625969433f,0.16413241657148153f,-0.027926657878934724f),
                        BlockType = GyroidBlockType.CD,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EsD, CornerSiteType.BottomLeft), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomLeft,
                        Bondee = CornerSiteType.TopLeft,
                        DeltaPosition = new Vector3(-1.0843171979573831f,-2.51906794607825f,0.25156902590518104f),
                        DeltaUp = new Vector3(0.7115750838347439f,-0.3136165689896662f,-0.14711541170046155f),
                        DeltaForward = new Vector3(0.3653265369349534f,-0.1798990968309545f,-0.08683364608392749f),
                        BlockType = GyroidBlockType.GEs,
                        isTail = false
                    }
                },
                { (GyroidBlockType.EsD, CornerSiteType.BottomRight), new GyroidBondMateData
                    {
                        Substrate = CornerSiteType.BottomRight,
                        Bondee = CornerSiteType.BottomLeft,
                        DeltaPosition = new Vector3(1.48563228726296f,-2.0329290166340943f,-0.4318131593110813f),
                        DeltaUp = new Vector3(0.9002139335878009f,-1.3392032196436703f,-0.26067456401816635f),
                        DeltaForward = new Vector3(0.18420216838353512f,-0.26985141094439846f,-0.05502955288667042f),
                        BlockType = GyroidBlockType.EsC,
                        isTail = true
                    }
                },

            };
        }


        public static GyroidBondMateData GetBondMateData(GyroidBlockType blockType, CornerSiteType siteType)
        {
            if (BondMateDataMap.TryGetValue((blockType, siteType), out var bondMateData))
            {
                return bondMateData;
            }

            throw new KeyNotFoundException($"No data found for block type {blockType} and site type {siteType}.");
            // Alternatively, return a default GyroidBondMateData or handle it as per your application's needs.
        }
    }
}

