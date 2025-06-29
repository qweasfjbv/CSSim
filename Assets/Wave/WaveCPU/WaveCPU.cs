using Sim.Util;
using System.Collections.Generic;
using UnityEngine;

namespace Sim
{
	public class WaveCPU : MonoBehaviour
	{
		public Material material;
		public int resolutionX = 64;
		public int resolutionZ = 64;
		public float spacing = 0.5f;
		public float tension = 1.0f;
		public float damping = 0.995f;
		public float impulseStrength = 1.0f;
		public float wavelengthFactor = 1.0f;
		public float waveInterval = 0.15f;
		public float restoreStrength = 0.01f;

		public float fixedDeltaTime = 0.05f;

		private ComputeBuffer matrixBuffer;

		private WavePoint[] currentWavePoints;
		private WavePoint[] nextWavePoints;
		private Matrix4x4[] matrices;

		public int Count { get => resolutionX * resolutionZ; }
		
		void Start()
		{
			InitArrays();
			InitBuffers();
			InitShaders();
		}

		void Update()
		{
			UpdateWaveCPU();
			UpdateMaterialParamsCPU();
			GenerateMeshCPU();
			GenerateImpulse();
		}

		private void InitArrays()
		{
			matrices = new Matrix4x4[Count];
			currentWavePoints = new WavePoint[Count];
			nextWavePoints = new WavePoint[Count];
		}

		private void InitBuffers()
		{
			matrixBuffer = new ComputeBuffer(Count, sizeof(float) * 16, ComputeBufferType.Default);
		}

		private void InitShaders()
		{
			Texture2D gradientTex = TextureGenerator.GenerateGradientTexture();
			material.SetTexture("_GradientTex", gradientTex);
			material.SetFloat("_EdgeLength", resolutionX * spacing);
		}

		private void UpdateWaveCPU()
		{
			for (int z = 0; z < resolutionZ; z++)
				for (int x = 0; x < resolutionX; x++)
				{
					int index = x + z * resolutionX;
					WavePoint self = currentWavePoints[index];
					float sum = 0;
					int count = 0;

					if (x > 0)
					{
						sum += currentWavePoints[(x - 1) + z * resolutionX].height;
						count++;
					}
					if (x < resolutionX - 1)
					{
						sum += currentWavePoints[(x + 1) + z * resolutionX].height;
						count++;
					}
					if (z > 0)
					{
						sum += currentWavePoints[x + (z - 1) * resolutionX].height;
						count++;
					}
					if (z < resolutionZ - 1)
					{
						sum += currentWavePoints[x + (z + 1) * resolutionX].height;
						count++;
					}

					float laplacian = (sum - count * self.height);
					float acceleration = tension * laplacian;

					acceleration += -self.height * restoreStrength;
					self.velocity += acceleration * fixedDeltaTime * wavelengthFactor;
					self.velocity *= damping;
					self.height += self.velocity * fixedDeltaTime;
					nextWavePoints[index] = self;
				}

			// Buffer swap
			WavePoint[] tmpPoints = currentWavePoints;
			currentWavePoints = nextWavePoints;
			nextWavePoints = tmpPoints;
		}

		private void UpdateMaterialParamsCPU()
		{
			for (int z = 0; z < resolutionZ; z++)
				for (int x = 0; x < resolutionX; x++)
				{
					int i = x + z * resolutionX;
					Vector3 pos = new Vector3(x * spacing, currentWavePoints[i].height, z * spacing);
					matrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
				}

			matrixBuffer.SetData(matrices);
			material.SetBuffer("matrixBuffer", matrixBuffer);
		}

		private void GenerateMeshCPU()
		{
			Vector3[] vertices = new Vector3[resolutionX * resolutionZ];
			for (int z = 0; z < resolutionZ; z++)
				for (int x = 0; x < resolutionX; x++)
				{
					int i = x + z * resolutionX;
					float height = currentWavePoints[i].height;
					vertices[i] = new Vector3(x * spacing, height, z * spacing);
				}

			List<int> indices = new List<int>();
			for (int z = 0; z < resolutionZ - 1; z++)
				for (int x = 0; x < resolutionX - 1; x++)
				{
					int i = x + z * resolutionX;
					int iRight = i + 1;
					int iBelow = i + resolutionX;
					int iBelowRight = i + resolutionX + 1;

					// Triangle 1
					indices.Add(i);
					indices.Add(iBelow);
					indices.Add(iRight);

					// Triangle 2
					indices.Add(iRight);
					indices.Add(iBelow);
					indices.Add(iBelowRight);
				}
			DrawWaveMesh(vertices, indices.ToArray());
		}

		private void DrawWaveMesh(Vector3[] vertices, int[] indices)
		{
			Mesh waveMesh = new Mesh();
			waveMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			waveMesh.vertices = vertices;
			waveMesh.triangles = indices;
			waveMesh.RecalculateNormals();
			Graphics.DrawMesh(waveMesh, Matrix4x4.identity, material, 0);
		}

		private float elapsedTime = 0f;
		private void GenerateImpulse()
		{
			elapsedTime += fixedDeltaTime;
			if (elapsedTime < waveInterval) return;

			elapsedTime = 0f;
			int centerIdx = Random.Range(0, resolutionX) + Random.Range(0, resolutionZ) * resolutionX;
			currentWavePoints[centerIdx].velocity += impulseStrength;
		}

		void OnDestroy()
		{
			matrixBuffer?.Dispose();
		}
	}
}