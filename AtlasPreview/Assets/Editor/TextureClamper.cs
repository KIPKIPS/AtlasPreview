using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
namespace EditorTools.UI {
    public class TextureClamper {
        //做图集的时候，给单元图片四周补2像素，否则界面缩放时会出现黑标或透明边的现象
        public const int BORDER = 2;
        public static Texture2D Clamp(Texture2D sourceTexture) {
            int sourceWidth = sourceTexture.width; //贴图宽高
            int sourceHeight = sourceTexture.height;
            //Texture2D.GetPixels32()返回的数组是二维数组，像素布局从左到右，从底到顶（即，一行行），数组的大小是使用mip level的宽乘高。默认的mip level为0（基本纹理），这时候数组的大小是纹理的大小。
            //在一般情况下，mip level大小是mipWidth=max(1,width>>miplevel) ，高度也同样。
            Color32[] sourcePixels = sourceTexture.GetPixels32();
            int targetWidth = sourceWidth + BORDER * 2;
            int targetHeight = sourceHeight + BORDER * 2;//外围补2 pixel
            Color32[] targetPixels = new Color32[targetWidth * targetHeight]; //像素数组
            Texture2D targetTexture = new Texture2D(targetWidth, targetHeight); //按照贴图大小创建一个Texture对象
            for (int i = 0; i < sourceHeight; i++) { //遍历源贴图的高
                for (int j = 0; j < sourceWidth; j++) { //遍历源贴图的宽
                    targetPixels[(i + BORDER) * targetWidth + (j + BORDER)] = sourcePixels[i * sourceWidth + j]; //这一步将源贴图的像素映射到了目标生成贴图的最中心,即外围包裹2 pixel
                }
            }
            //上下左右四周各补2像素源贴图的边缘临界像素值
            //左边缘
            for (int v = 0; v < sourceHeight; v++) {
                for (int k = 0; k < BORDER; k++) {
                    targetPixels[(v + BORDER) * targetWidth + k] = sourcePixels[v * sourceWidth];
                }
            }
            //右边缘
            for (int v = 0; v < sourceHeight; v++) {
                for (int k = 0; k < BORDER; k++) {
                    targetPixels[(v + BORDER) * targetWidth + (sourceWidth + BORDER + k)] = sourcePixels[v * sourceWidth + sourceWidth - 1];
                }
            }
            //上边缘
            for (int h = 0; h < sourceWidth; h++) {
                for (int k = 0; k < BORDER; k++) {
                    targetPixels[(sourceHeight + BORDER + k) * targetWidth + BORDER + h] = sourcePixels[(sourceHeight - 1) * sourceWidth + h];
                }
            }
            //下边缘
            for (int h = 0; h < sourceWidth; h++) {
                for (int k = 0; k < BORDER; k++) {
                    targetPixels[k * targetWidth + BORDER + h] = sourcePixels[h];
                }
            }
            targetTexture.SetPixels32(targetPixels); //为贴图设置像素信息,自动将一维的像素数组转化成二维的贴图信息数组
            targetTexture.Apply();//实际应用任何先前的 SetPixel 和 SetPixels 更改,将贴图数据进行应用
            return targetTexture; //返回targetTexture贴图数据
        }
        public static Texture2D ClampSingle(Texture2D sourceTexture) {
            int sourceWidth = sourceTexture.width;
            int sourceHeight = sourceTexture.height;
            Color32[] sourcePixels = sourceTexture.GetPixels32();
            int targetWidth = sourceWidth;
            int targetHeight = sourceHeight;
            Texture2D targetTexture = new Texture2D(targetWidth, targetHeight);
            targetTexture.SetPixels32(sourcePixels);
            targetTexture.Apply();
            return targetTexture;
        }
    }
}