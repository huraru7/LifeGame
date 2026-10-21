#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0
	#define PS_SHADERMODEL ps_4_0
#endif

// 1セル1ピクセルの画像(ミップマップつき)から盤面を描く。
// テクスチャ座標は 0〜1 の外にもなり、サンプラーの Wrap で盤面が繰り返される(どこまでも続いて見える)。
// 拡大時: セルを隙間つきの四角として描き、縁は1ピクセル幅で滑らかにする。
// 縮小時: 画面の1ピクセルが覆う範囲の生存密度を、ミップマップから読んで明るさにする(砂嵐やチラつきが出ない)。

float4x4 MatrixTransform;
float2 BoardSize;
float4 AliveColor;
float4 DeadColor;
float4 GapColor;

Texture2D SpriteTexture;
sampler2D CellSampler = sampler_state
{
	Texture = <SpriteTexture>;
	MinFilter = Linear;
	MagFilter = Point;
	MipFilter = Linear;
	AddressU = Wrap;
	AddressV = Wrap;
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
	float2 uv = input.TextureCoordinates;
	float2 cellPos = uv * BoardSize;

	// 画面の1ピクセルが覆うセル数
	float2 gx = ddx(cellPos);
	float2 gy = ddy(cellPos);
	float cellsPerPixel = max(max(length(float2(gx.x, gy.x)), length(float2(gx.y, gy.y))), 0.00001);
	float pixelsPerCell = 1.0 / cellsPerPixel;

	// ミップマップの段は「1ピクセルに何セル入るか」の log2。0未満(拡大)なら最も細かい段を点で読む
	float density = tex2Dlod(CellSampler, float4(uv, 0.0, log2(cellsPerPixel))).r;
	// 縮小時は、まばらな領域も見えるように密度の低いところを持ち上げる
	float brightness = cellsPerPixel > 1.0 ? saturate(sqrt(density) * 1.15) : density;
	float4 cellColor = lerp(DeadColor, AliveColor, brightness);

	// セルが4ピクセルを超えたあたりから、隙間を少しずつ見せる
	float tileAmount = saturate((pixelsPerCell - 4.0) / 4.0);
	float2 local = frac(cellPos) * pixelsPerCell;
	float gap = clamp(pixelsPerCell * 0.1, 1.0, 3.0);
	float2 toEdge = min(local, pixelsPerCell - local) - gap * 0.5;
	float inside = saturate(min(toEdge.x, toEdge.y) + 0.5);

	float4 color = lerp(cellColor, lerp(GapColor, cellColor, inside), tileAmount);
	return color * input.Color;
}

technique BoardDisplay
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
