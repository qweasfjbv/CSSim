using UnityEngine;

namespace Sim.Util
{
	public static class TextureGenerator
	{
		public static Texture2D GenerateGradientTexture()
		{
			Texture2D tex = new Texture2D(256, 256);
			for (int y = 0; y < 256; y++)
				for (int x = 0; x < 256; x++)
				{
					float u = x / 255f;
					float v = y / 255f;
					Color color = MyGradient2D(u, v);
					tex.SetPixel(x, y, color);
				}
			tex.Apply();
			return tex;
		}

		private static Color MyGradient2D(float u, float v)
		{
			Color bottomLeft = Color.red;
			Color bottomRight = Color.green;
			Color topLeft = Color.blue;
			Color topRight = Color.yellow;

			Color bottom = Color.Lerp(bottomLeft, bottomRight, u);
			Color top = Color.Lerp(topLeft, topRight, u);
			return Color.Lerp(bottom, top, v);
		}
	}
}