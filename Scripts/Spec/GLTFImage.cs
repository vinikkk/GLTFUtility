﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Siccity.GLTFUtility {
	// https://github.com/KhronosGroup/glTF/blob/master/specification/2.0/README.md#image
	public class GLTFImage {
		/// <summary>
		/// The uri of the image.
		/// Relative paths are relative to the .gltf file.
		/// Instead of referencing an external file, the uri can also be a data-uri.
		/// The image format must be jpg or png.
		/// </summary>
		public string uri;
		public string mimeType;
		public int? bufferView;
		public string name;

		public class ImportResult {
			public Texture2D texture;
			/// <summary> True if image was loaded from a Texture2D asset. False if it was loaded from binary or from another source </summary>
			public bool isNormalMap;
			public bool isMetallicRoughnessFixed;
			public byte[] bytes;
			public string mimeType;

			public Texture2D GetNormalMap() {
				if (isNormalMap) return texture;
				Color32[] pixels = texture.GetPixels32();
				for (int i = 0; i < pixels.Length; i++) {
					Color32 c = pixels[i];
					c.a = pixels[i].r;
					c.r = c.b = c.g;
					pixels[i] = c;
				}
				texture.SetPixels32(pixels);
				texture.Apply();
				isNormalMap = true;
				return texture;
			}

			// glTF stores Metallic in blue channel and roughness in green channel. Unity stores Metallic in red and roughness in alpha. This method returns a unity-fixed texture
			public Texture2D GetFixedMetallicRoughness() {
				if (!isMetallicRoughnessFixed) {
					Color32[] pixels = texture.GetPixels32();
					for (int i = 0; i < pixels.Length; i++) {
						Color32 c = pixels[i];
						c.r = pixels[i].b;
						c.a = pixels[i].g;
						pixels[i] = c;
					}
					texture.SetPixels32(pixels);
					texture.Apply();
					isMetallicRoughnessFixed = true;
				}
				return texture;
			}
		}

		public ImportResult GetImage(string directoryRoot, GLTFBufferView.ImportResult[] bufferViews) {
			ImportResult result = new ImportResult();

			if (!string.IsNullOrEmpty(uri) && File.Exists(directoryRoot + uri)) {
				byte[] fileData = File.ReadAllBytes(directoryRoot + uri);
				result.bytes = fileData;
				result.mimeType = mimeType;
				return result;
			} else if (bufferView.HasValue && !string.IsNullOrEmpty(mimeType)) {
				byte[] bytes = bufferViews[bufferView.Value].bytes;
				result.bytes = bytes;
				result.mimeType = mimeType;
				return result;
			} else {
				Debug.Log("Couldn't find texture at " + directoryRoot + uri);
				return null;
			}
		}

		public class ImportTask : Importer.ImportTask {
			public override Task Task { get { return task; } }
			public Task<ImportResult[]> task;

			public ImportTask(List<GLTFImage> images, string directoryRoot, GLTFBufferView.ImportTask bufferViewTask) : base(bufferViewTask) {
				task = new Task<ImportResult[]>(() => {
					if (images == null) return new ImportResult[0];

					ImportResult[] results = new ImportResult[images.Count];
					for (int i = 0; i < results.Length; i++) {
						results[i] = images[i].GetImage(directoryRoot, bufferViewTask.task.Result);
					}
					return results;
				});
			}

			protected override void OnCompleted() {
				foreach (ImportResult result in task.Result) {
					result.texture = new Texture2D(2, 2);
					if (!result.texture.LoadImage(result.bytes)) {
						Debug.Log("mimeType not supported: " + result.mimeType);
					}
				}
			}
		}
	}
}