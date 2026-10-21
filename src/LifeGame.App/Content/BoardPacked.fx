#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0
	#define PS_SHADERMODEL ps_4_0
#endif

// 盤面のビットを詰めたテクスチャ(1ピクセル = RGBAの4バイト = 32セル)を、ピクセルごとに展開して描く。
// ビット演算が使えないシェーダモデルなので、バイト値から割り算と剰余でビットを取り出す。

float4x4 MatrixTransform;
float2 BoardSize;    // 盤面のセル数 (幅, 高さ)
float PackedWidth;   // テクスチャの横幅 (幅 / 32)
float4 AliveColor;
float4 DeadColor;
float4 GridColor;

Texture2D SpriteTexture;
sampler2D BoardSampler = sampler_state
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

// 整数のセル座標 cell が生きていれば1、死んでいれば0を返す
float CellAt(float2 cell)
{
	float texelX = floor(cell.x / 32.0);
	float2 uv = float2((texelX + 0.5) / PackedWidth, (cell.y + 0.5) / BoardSize.y);
	float4 texel = tex2Dlod(BoardSampler, float4(uv, 0.0, 0.0));

	float inTexel = cell.x - texelX * 32.0;
	float channel = floor(inTexel / 8.0);
	float bitIndex = inTexel - channel * 8.0;

	// 該当するチャンネル(R=先頭の8セル、G、B、A)だけ1になるベクトルで、バイト値を取り出す
	float4 channelMask = saturate(1.0 - abs(float4(0.0, 1.0, 2.0, 3.0) - channel));
	float byteValue = floor(dot(texel, channelMask) * 255.0 + 0.5);

	// exp2 の誤差で切り捨てが1つずれないよう、わずかに足してから切り捨てる
	return fmod(floor(byteValue / exp2(bitIndex) + 0.001), 2.0);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	float2 cellPos = input.TextureCoordinates * BoardSize;
	// 画面の1ピクセルが覆うセル数
	float2 footprint = float2(
		length(float2(ddx(cellPos.x), ddy(cellPos.x))),
		length(float2(ddx(cellPos.y), ddy(cellPos.y))));
	float cellsPerPixel = max(footprint.x, footprint.y);
	float2 maxCell = BoardSize - 1.0;

	float alive;
	if (cellsPerPixel <= 1.0)
	{
		alive = CellAt(clamp(floor(cellPos), 0.0, maxCell));
	}
	else
	{
		// 縮小表示: ピクセルが覆う範囲を3×3点で標本化し、生存密度にする(間引きによるチラつきを抑える)
		float sum = 0.0;
		[unroll] for (int sy = 0; sy < 3; sy++)
		{
			[unroll] for (int sx = 0; sx < 3; sx++)
			{
				float2 offset = (float2(sx, sy) + 0.5) / 3.0 - 0.5;
				sum += CellAt(clamp(floor(cellPos + offset * footprint), 0.0, maxCell));
			}
		}
		// 密度が低くても見えるように持ち上げる
		alive = sqrt(sum / 9.0);
	}
	float4 color = lerp(DeadColor, AliveColor, alive);

	// 拡大時はセルの境目に線を引く。セルが小さいうちは線を薄くする
	float pixelsPerCell = 1.0 / cellsPerPixel;
	float2 distanceToEdge = min(frac(cellPos), 1.0 - frac(cellPos)) * pixelsPerCell;
	float gridAmount = 1.0 - saturate(min(distanceToEdge.x, distanceToEdge.y) - 0.5);
	gridAmount *= saturate((pixelsPerCell - 6.0) / 6.0);

	return lerp(color, GridColor, gridAmount) * input.Color;
}

technique BoardPacked
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
