#define NONE 0
#define HUED 1
#define PARTIAL 2

static const float TileSize = 31.11;
static const float3 LIGHT_DIRECTION = float3(0.0f, 1.0f, 1.0f);
static const float Brightlight = 1.5f; //This can be parametrized, but 1.5f is default :)

sampler TextureSampler : register(s0);
sampler HueSampler : register(s1);

//Effect parameters
float4x4 WorldViewProj;
int HueCount;
float4 VirtualLayerFillColor;
float4 VirtualLayerBorderColor;
float LightLevel;

/* For now, all the techniques use the same vertex definition */
struct VSInput {
    float4 Position : SV_Position;
    float3 TexCoord : TEXCOORD0;
    float3 HueCoord : TEXCOORD1;
};

struct VSOutput {
    float4 OutputPosition : SV_Position;
    float3 TexCoord       : TEXCOORD0;
    float3 HueCoord       : TEXCOORD1;
};

struct PSInput {
    float3 TexCoord       : TEXCOORD0;
    float3 HueCoord       : TEXCOORD1;
};

bool is_zero_vector(float3 v)
{   
    return v.x == 0 && v.y == 0 && v.z == 0;
}

//Thanks ClassicUO
float get_light(float3 norm)
{
	float3 light = normalize(LIGHT_DIRECTION);
	float3 normal = normalize(norm);
	float base = (max(dot(normal, light), 0.0f) / 2.0f) + 0.5f;

	// At 45 degrees (the angle the flat tiles are lit at) it must come out
	// to (cos(45) / 2) + 0.5 or 0.85355339...
	return base + ((Brightlight * (base - 0.85355339f)) - (base - 0.85355339f));
}


//Common vertex shader
VSOutput TileVSMain(VSInput vin) {
    VSOutput vout;

    vout.OutputPosition = mul(vin.Position, WorldViewProj);
    vout.OutputPosition.z += vin.TexCoord.z;
    vout.TexCoord = vin.TexCoord;
    vout.HueCoord = vin.HueCoord;

    return vout;
}

float4 TerrainPSMain(PSInput pin) : SV_Target0
{
    float4 color = tex2D(TextureSampler, pin.TexCoord.xy);
    if (color.a == 0)
        discard;
        
    // We use TexCoord.z to tell shader if it uses TexMap or Art and based on this we apply lighting or not
    // Landtiles in Art come with lighting prebaked into it
    if(pin.TexCoord.z > 0.0f) 
        color.rgb *= get_light(pin.HueCoord);
        
    color.rgb *= LightLevel;
    
    return color;
}

float4 StaticsPSMain(PSInput pin) : SV_Target0
{
    float4 color = tex2D(TextureSampler, pin.TexCoord.xy);
    if (color.a == 0)
        discard;
        
    int mode = int(pin.HueCoord.y);
        
    if (mode == HUED || (mode == PARTIAL && color.r == color.g && color.r == color.b))
    {
        float2 hueCoord = float2(color.r, pin.HueCoord.x / HueCount);
        color.rgb = tex2D(HueSampler, hueCoord).rgb;
    }

    color.a = pin.HueCoord.z;

    color.rgb *= LightLevel;
  
    return color;
}

float4 SelectionPSMain(PSInput pin) : SV_Target0
{
    float4 color = tex2D(TextureSampler, pin.TexCoord.xy);
     if (color.a == 0)
            discard;
    return float4(pin.HueCoord, 1.0);
}

VSOutput VirtualLayerVSMain(VSInput vin) {
    VSOutput vout;
    
    vout.OutputPosition = mul(vin.Position, WorldViewProj);
    vout.TexCoord = vin.Position;
    vout.HueCoord = vin.HueCoord;
    
    return vout;
}

float4 VirtualLayerPSMain(PSInput pin) : SV_Target0
{
    //0.7 worked for me as it's not glitching when moving camera
    if (abs(fmod(pin.TexCoord.x, TileSize)) < 0.7 || abs(fmod(pin.TexCoord.y, TileSize)) < 0.7) 
    {
            return VirtualLayerBorderColor;
    } 
    else 
    {
            return VirtualLayerFillColor;
    }
}

Technique Terrain
{
    Pass
    {
        VertexShader = compile vs_2_0 TileVSMain();
        PixelShader = compile ps_2_0 TerrainPSMain();
    }
}

Technique Statics {
    Pass
    {
        VertexShader = compile vs_2_0 TileVSMain();
        PixelShader = compile ps_2_0 StaticsPSMain();
    }
}

Technique Selection {
    Pass
    {
        VertexShader = compile vs_2_0 TileVSMain();
        PixelShader = compile ps_2_0 SelectionPSMain();
    }
}

Technique VirtualLayer {
    Pass
    {
        VertexShader = compile vs_2_0 VirtualLayerVSMain();
        PixelShader = compile ps_2_0 VirtualLayerPSMain();
    }
}