using System.Numerics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

public class MandelbrotJobs : MonoBehaviour
{
	double height, width;
	double rStart, iStart;
	int maxIterations;
	int zoom;

	Texture2D display;
	public Image image;

	public TextMeshProUGUI timePerFrame;

	Color32[] colors;

	[BurstCompile]
	struct Mandelbrot : IJobParallelFor
    {
		public NativeArray<double> x;
		public NativeArray<double> y;
		public int maxIterations;
		public NativeArray<int> result;

		public void Execute(int r)
        {
			result[r] = 0;
			Complex z = new Complex(0, 0);

			//Cardiod-Bulb Checking
			double q = (x[r] - 0.25) * (x[r] - 0.25) + y[r] * y[r];
			if (q * (q + (x[r] - 0.25)) < 0.25 * (y[r] * y[r]))
			{
				result[r] = maxIterations;
				return;
			}

			for (int i = 0; i != maxIterations; i++)
			{
				z = z * z + new Complex(x[r], y[r]);

				if (Complex.Abs(z) > 2)
				{
					return;
				}
				else
				{
					result[r]++;
				}
			}
		}
    }

	[BurstCompile]
struct SetColor : IJobParallelFor
{
    public NativeArray<int> value;
    public int maxIterations;
    public NativeArray<Color32> colors;

    public void Execute(int r)
    {
        Color32 color = new Color32(0, 0, 0, 255);

        int iterations = value[r];
        if (iterations != maxIterations)
        {
            int colorNr = iterations % 16;
            byte r1, g1, b1;

            // Precompute color values
            switch (colorNr)
            {
                case 0: r1 = 66; g1 = 30; b1 = 15; break;
                case 1: r1 = 25; g1 = 7; b1 = 26; break;
                case 2: r1 = 9; g1 = 1; b1 = 47; break;
                case 3: r1 = 4; g1 = 4; b1 = 73; break;
                case 4: r1 = 0; g1 = 7; b1 = 100; break;
                case 5: r1 = 12; g1 = 44; b1 = 138; break;
                case 6: r1 = 24; g1 = 82; b1 = 177; break;
                case 7: r1 = 57; g1 = 125; b1 = 209; break;
                case 8: r1 = 134; g1 = 181; b1 = 229; break;
                case 9: r1 = 211; g1 = 236; b1 = 248; break;
                case 10: r1 = 241; g1 = 233; b1 = 191; break;
                case 11: r1 = 248; g1 = 201; b1 = 95; break;
                case 12: r1 = 255; g1 = 170; b1 = 0; break;
                case 13: r1 = 204; g1 = 128; b1 = 0; break;
                case 14: r1 = 153; g1 = 87; b1 = 0; break;
                case 15: r1 = 106; g1 = 52; b1 = 3; break;
                default: r1 = 0; g1 = 0; b1 = 0; break;
            }

            color = new Color32(r1, g1, b1, 255);
        }

        colors[r] = color;
    }
}

	// Start is called before the first frame update
	void Start()
	{
		width = 4.5;
		height = width * Screen.height / Screen.width;
		rStart = -2.0;
		iStart = -1.25;
		zoom = 10;
		maxIterations = 500;

		display = new Texture2D(Screen.width, Screen.height);
		colors = new Color32[Screen.width * Screen.height];
		RunMandelbrot();
	}

	// Update is called once per frame
	void Update()
	{
		if (Input.GetMouseButtonDown(0))
		{
			rStart = rStart + (Input.mousePosition.x - (Screen.width / 2.0)) / Screen.width * width;
			iStart = iStart + (Input.mousePosition.y - (Screen.height / 2.0)) / Screen.height * height;
			RunMandelbrot();
		}

		if (Input.mouseScrollDelta.y != 0)
		{
			double wFactor = width * (double)Input.mouseScrollDelta.y / zoom;
			double hFactor = height * (double)Input.mouseScrollDelta.y / zoom;
			width -= wFactor;
			height -= hFactor;
			rStart += wFactor / 2.0;
			iStart += hFactor / 2.0;

			if (Input.mouseScrollDelta.y > 0)
            {
				maxIterations += 3;
			}
            else
            {
				maxIterations -= 3;
            }

			RunMandelbrot();
		}
	}

	void RunMandelbrot()
	{
		float startTime = Time.realtimeSinceStartup;

		NativeArray<int> result = new NativeArray<int>(display.width * display.height, Allocator.TempJob);
		NativeArray<Color32> color = new NativeArray<Color32>(display.width * display.height, Allocator.TempJob);
		NativeArray<double> xList = new NativeArray<double>(display.width * display.height, Allocator.TempJob);
		NativeArray<double> yList = new NativeArray<double>(display.width * display.height, Allocator.TempJob);

		for (int y = 0; y != display.height; y++)
		{
			for (int x = 0; x != display.width; x++)
			{
				xList[x + y * display.width] = rStart + width * (double)x / display.width;
				yList[x + y * display.width] = iStart + height * (double)y / display.height;
			}
		}

		Mandelbrot mandelbrot = new Mandelbrot()
		{
			x = xList,
			y = yList,
			maxIterations = maxIterations,
			result = result
		};

		JobHandle handle = mandelbrot.Schedule(result.Length, 10);
		handle.Complete();

		SetColor setColor = new SetColor()
		{
			value = result,
			maxIterations = maxIterations,
			colors = color
		};

		JobHandle handle2 = setColor.Schedule(result.Length, 10, handle);
		handle2.Complete();

		for (int y = 0; y != display.height; y++)
		{
			for (int x = 0; x != display.width; x++)
			{
				colors[x + y * display.width] = color[x + y * display.width];
			}
		}

		display.SetPixels32(colors);

		display.Apply();
		image.sprite = Sprite.Create(display, new Rect(0, 0, display.width, display.height),
			new UnityEngine.Vector2(0.5f, 0.5f));

		float endTime = Time.realtimeSinceStartup;
		timePerFrame.text = (endTime - startTime).ToString();

		result.Dispose();
		color.Dispose();
		xList.Dispose();
		yList.Dispose();
	}
}