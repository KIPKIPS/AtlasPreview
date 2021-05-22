using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.IO;
namespace EditorTools.UI {
    //图集预览工具

    public class AtlasPreviewer : EditorWindow {
        public const int ATLAS_MAX_SIZE = 2048;
        public const int FAVOR_ATLAS_SIZE = 1024;
        private static AtlasPreviewer _window;
        private static Texture2D _atlas;
        [MenuItem("Assets/预览图集", false, 102)]
        public static void PreviewAtlas() {
            const string basePath = "Assets/UI";//UI资源基础目录
            string[] aryAssetGuids = Selection.assetGUIDs;//选中对象的guid
            if (aryAssetGuids != null && aryAssetGuids.Length > 0) { //有选中
                string folderPath = AssetDatabase.GUIDToAssetPath(aryAssetGuids[0]);//资源所在的目录 Assets/Things/Textures/UI/xxx
                if (folderPath == basePath) { //不能是资源根目录
                    Debug.Log("请选择[{0}]的子目录:" + basePath);
                    _atlas = null;
                } else if (folderPath.StartsWith(basePath)) { //以UI资源基础目录为开始前缀
                    if (HasSubDirectory(folderPath)) { //选中的目录内包含多个目录
                        _atlas = null;
                        Debug.Log("此目录下还有子目录，请选择只包含图片的子目录");
                    } else {
                        string[] assetPaths = GetAssetPaths(folderPath); //获取目录下所有资源的路径
                        TextureData[] textureDatas = ReadTextures(assetPaths);//读取所有资源路径对应的资源到TextureData数组
                        if (MaxRectsBinPack.IsUseMaxRectsAlgo) { //使用MaxRect算法
                            _atlas = CreateAtlasMaxRect(textureDatas);//使用MaxRect算法来创建图集
                        } else {
                            _atlas = CreateAtlas(textureDatas);//unity Texture2D自带的图集合并
                        }
                    }
                } else {
                    Debug.Log("请选择[{0}]的子目录:" + basePath);//选择了奇怪的目录
                    _atlas = null;
                }
            }
            _window = EditorWindow.GetWindow<AtlasPreviewer>("图集预览"); //获取EditorWindow
            _window.Show();//显示
            _window.position = new Rect(1920 / 2 - 250, 1080 / 2 - 350, 500, 600);//显示的位置
        }
        private static string[] GetAssetPaths(string folderPath) {
            string systemPath = ToFileSystemPath(folderPath);
            string[] filePaths = Directory.GetFiles(systemPath, "*.png");
            string[] result = new string[filePaths.Length];
            for (int i = 0; i < filePaths.Length; i++) {
                result[i] = ToAssetPath(filePaths[i]);
            }
            return result;
        }
        //是否包含子文件夹
        private static bool HasSubDirectory(string folderPath) {
            string systemPath = ToFileSystemPath(folderPath); //获取到存储磁盘的物理路径
            string[] dirPaths = Directory.GetDirectories(systemPath);// 获取目录下的子目录
            return dirPaths != null && dirPaths.Length > 0;//子目录不为空 且子目录长度大于等于1
        }
        //读取一个图片资源路径列表的数据,返回TextureData数组
        private static TextureData[] ReadTextures(string[] assetPaths) {
            TextureData[] textureDatas = new TextureData[assetPaths.Length];//创建TextureData数组
            for (int i = 0; i < assetPaths.Length; i++) { //遍历路径列表
                Sprite sprite = AssetDatabase.LoadAssetAtPath(assetPaths[i], typeof(Sprite)) as Sprite; //加载图片资源 Sprite
                TextureData data = new TextureData();//创建TextureData对象
                if (sprite == null) { //Sprite不存在则报错
                    Debug.LogErrorFormat("ReadTextures sprite is null at assetPaths[{0}]", assetPaths[i]);
                }
                data.name = sprite.name; //赋予名称
                Vector4 border = sprite.border; //v4变量 返回sprite的边框的大小 X=左边框/Y=下边框/Z=右边框/W=上边框
                data.top = (int)border.w;
                data.right = (int)border.z;
                data.bottom = (int)border.y;
                data.left = (int)border.x;
                Texture2D texture = AssetDatabase.LoadAssetAtPath(assetPaths[i], typeof(Texture2D)) as Texture2D;
                if (textureDatas.Length > 1) { //包含多个图片资源
                    texture = TextureClamper.Clamp(texture);//给每个单元图片四周补两像素
                } else {
                    texture = TextureClamper.ClampSingle(texture);//单图不进行外边框的2像素填充
                }
                data.texture = texture;//texture数据赋给TextureData的texture属性
                data.width = texture.width;//宽
                data.height = texture.height;//高
                textureDatas[i] = data;//存储下来
            }
            return textureDatas;//将处理好的TextureData数组返回
        }
        private static Texture2D CreateAtlas(TextureData[] textureDatas) {
            Texture2D atlas = new Texture2D(ATLAS_MAX_SIZE, ATLAS_MAX_SIZE);//创建图集按照最大尺寸
            atlas.PackTextures(GetPackTextures(textureDatas), 0, ATLAS_MAX_SIZE, false);//传入纹理数组,纹理间距0,最大尺寸,不关闭可读
            return atlas;//返回填充的图集,本质上为一个合并好的texture2D
        }
        //根据贴图数组来创建合并好的贴图图集
        private static Texture2D CreateAtlasMaxRect(TextureData[] textureDatas) {
            Array.Sort<TextureData>(textureDatas, new Texture2DComparison());//读贴图数据进行排序
            Texture2D atlas = new Texture2D(ATLAS_MAX_SIZE, ATLAS_MAX_SIZE);//创建Texture2D图集,按照最大尺寸
            MaxRectsBinPack.PackTextures(atlas, GetPackTextures(textureDatas));//将贴图数据(每个sprite)填充进atlas图集
            return atlas;
        }
        //获取打包的texture2D贴图数据数组
        private static Texture2D[] GetPackTextures(TextureData[] textureDatas) {
            Texture2D[] result = new Texture2D[textureDatas.Length];//创建贴图数组
            for (int i = 0; i < textureDatas.Length; i++) {//遍历贴图数据数组
                result[i] = textureDatas[i].texture;//把纹理数据保存下来
            }
            return result;
        }
        private void OnGUI() {
            try {
                ShowAtlasTexture();
            } catch (Exception e) {
                Debug.Log(e);
            }
        }
        private void OnDestroy() {
        }
        private static void ShowAtlasTexture() {
            if (_atlas == null) {
                return;
            }
            GUILayout.BeginHorizontal();
            Color back = GUI.color;
            GUI.color = Color.white;
            GUILayout.Label("生成图集尺寸： " + _atlas.width.ToString() + "x" + _atlas.height.ToString(), EditorStyles.whiteLabel);
            GUI.color = back;
            GUILayout.EndHorizontal();
            ShowTexture(_atlas);
        }
        private static void ShowTexture(Texture2D texture) {
            int width = texture.width;
            int height = texture.height;
            float ratio = 1.0f;
            float previewSize = 512.0f;
            if (width > previewSize || height > previewSize) {
                if (width > height) {
                    ratio = previewSize / (float)width;
                } else {
                    ratio = previewSize / (float)height;
                }
            }
            GUILayout.Box(texture, GUILayout.Width(width * ratio), GUILayout.Height(height * ratio));
        }
        public static string ToFileSystemPath(string assetPath) {
            return Application.dataPath.Replace("Assets", "") + assetPath; //assetPath 资源路径
        }
        public static string ToAssetPath(string systemPath) {
            systemPath = systemPath.Replace("\\", "/");
            return "Assets" + systemPath.Substring(Application.dataPath.Length);
        }
    }
    public class TextureData {
        public string name;
        public Texture2D texture;
        public int width;
        public int height;
        public int top;
        public int right;
        public int bottom;
        public int left;
    }
}