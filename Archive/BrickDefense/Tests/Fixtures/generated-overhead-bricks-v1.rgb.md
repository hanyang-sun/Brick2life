# 原图颜色回归数据

`generated-overhead-bricks-v1.rgb.gz` 是同目录原始 AI 生成 PNG 的无损 RGB 解码数据，不修改图片，不增加或删除像素。

GZip 解压后：8 字节头（小端 Int32 宽、高，均为 1254），之后为从左上角逐行排列的 RGB24 字节。文件用于 .NET 检查，无需在 Unity 运行时加入图片解码依赖。Unity 集成检查直接读取原 PNG。

生成命令（需 Python Pillow）：

```powershell
python -c "from PIL import Image; import struct,gzip; im=Image.open('Tests/Fixtures/generated-overhead-bricks-v1.png').convert('RGB'); gzip.open('Tests/Fixtures/generated-overhead-bricks-v1.rgb.gz','wb').write(struct.pack('<II',*im.size)+im.tobytes())"
```

固定标定坐标和预期布局见 `Tests/PhotoColorRegressionChecks.cs`。该图不用于声明实拍准确率。
