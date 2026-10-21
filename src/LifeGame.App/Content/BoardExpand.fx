#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0
	#define PS_SHADERMODEL ps_4_0
#endif

// ビットを詰めたテクスチャ(1ピクセル = RGBAの4バイト = 32セル)を、1セル1ピクセルの画像へ展開する。
// 結果は R に生(1)か死(0)を持つ。この画像の縮小版(ミップマップ)が、縮小表示での生存密度になる。
// ビット演算が使えないシェーダモデルなので、バイト値から割り算と剰余でビットを取り出す。

float4x4 MatrixTransform;
float2 BoardSize;    // 盤面のセル数 (幅, 高さ)
float PackedWidth;   // 詰めたテクスチャの横幅 (幅 / 32)

Texture2D SpriteTexture;
sampler2D PackedSampler = sampler_state
{
	Texture = <SpriteTexture>;
	MinFilter = Point;
	MagFilter = Point;
	MipFilter = Point;
	AddressU = Clamp;
	AddressV = Clamp;
};

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
	VertexShaderOutput output;
	output.Position = mul(input.Position, MatrixTransform);
	output.Color = input.Color;
	output.TextureCoordinates = input.TextureCoordinates;
	return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	float2 cell = floor(input.TextureCoordinates * BoardSize);
	float texelX = floor(cell.x / 32.0);
	float2 uv = float2((texelX + 0.5) / PackedWidth, (cell.y + 0.5) / BoardSize.y);
	float4 texel = tex2Dlod(PackedSampler, float4(uv, 0.0, 0.0));

	float inTexel = cell.x - texelX * 32.0;
	float channel = floor(inTexel / 8.0);
	float bitIndex = inTexel - channel * 8.0;

	// 該当するチャンネル(R = 先頭の8セル、G、B、A)だけ1になるベクトルで、バイト値を取り出す
	float4 channelMask = saturate(1.0 - abs(float4(0.0, 1.0, 2.0, 3.0) - channel));
	float byteValue = floor(dot(texel, channelMask) * 255.0 + 0.5);

	// exp2 の誤差で切り捨てが1つずれないよう、わずかに足してから切り捨てる
	float alive = fmod(floor(byteValue / exp2(bitIndex) + 0.001), 2.0);
	return float4(alive, alive, alive, 1.0);
}

technique BoardExpand
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
