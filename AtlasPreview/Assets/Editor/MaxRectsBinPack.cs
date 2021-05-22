using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
namespace EditorTools.UI {
    //定义贴图数据的排序
    public class Texture2DComparison : IComparer<TextureData> {
        public int Compare(TextureData x, TextureData y) { //进行比较的贴图数据x和y
            int ret = 0;
            if (Mathf.Max(x.width, x.height) > Mathf.Max(y.width, y.height)) { //x宽高最大值 比 y宽高最大值 大
                ret = -1; //返回负数,代表x插入在y的前面
            } else if (Mathf.Max(x.width, x.height) < Mathf.Max(y.width + y.height)) { //x的宽高最大值小于y的宽高值
                ret = 1; //返回整数,代表将x插入在y的后面
            }
            return ret;//返回相等
        }
    }
    public class MaxRectsBinPack {
        public const int ATLAS_MAX_SIZE = 2048;
        public const int FAVOR_ATLAS_SIZE = 1024;
        //是否使用此算法进行图集合并
        public static bool IsUseMaxRectsAlgo = true;
        public int binWidth = 0; //容器的宽
        public int binHeight = 0; //容器的高
        public bool allowRotations; // 是否可旋转
        public List<Rect> usedRectangles = new List<Rect>(); //已经用掉的矩形列表
        public List<Rect> freeRectangles = new List<Rect>(); //空闲矩形列表
        public enum FreeRectChoiceHeuristic { //匹配规则
            RectBestShortSideFit, // BSSF: 短边最接近
            RectBestLongSideFit, // BLSF: 长边最接近
            RectBestAreaFit, // BAF: 面积最接近
            RectBottomLeftRule, /// BL: 放在最左下
            RectContactPointRule // CP: 尽可能与更多矩形相邻
        };
        public MaxRectsBinPack(int width, int height, bool rotations = true) { //roattions 是否可旋转
            Init(width, height, rotations);
        }
        public void Init(int width, int height, bool rotations = true) { //一些初始化操作,清空空闲矩形列表
            binWidth = width;
            binHeight = height;
            allowRotations = rotations;
            Rect n = new Rect(); //一个矩形,左下角起始,宽高
            n.x = 0;
            n.y = 0;
            n.width = width;
            n.height = height;
            usedRectangles.Clear();
            freeRectangles.Clear();
            freeRectangles.Add(n); //把初始化的矩形添加进来
        }
        public Rect Insert(int width, int height, FreeRectChoiceHeuristic method) {
            Rect newNode = new Rect();
            int score1 = 0; // Unused in this function. We don't need to know the score after finding the position.
            int score2 = 0;
            switch (method) { //根据匹配的方法进行四种匹配
                case FreeRectChoiceHeuristic.RectBestShortSideFit: newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2); break;
                case FreeRectChoiceHeuristic.RectBottomLeftRule: newNode = FindPositionForNewNodeBottomLeft(width, height, ref score1, ref score2); break;
                case FreeRectChoiceHeuristic.RectContactPointRule: newNode = FindPositionForNewNodeContactPoint(width, height, ref score1); break;
                case FreeRectChoiceHeuristic.RectBestLongSideFit: newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, ref score1); break;
                case FreeRectChoiceHeuristic.RectBestAreaFit: newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2); break;
            }
            if (newNode.height == 0)
                return newNode;
            int numRectanglesToProcess = freeRectangles.Count;
            for (int i = 0; i < numRectanglesToProcess; ++i) {
                if (SplitFreeNode(freeRectangles[i], ref newNode)) {
                    freeRectangles.RemoveAt(i);
                    --i;
                    --numRectanglesToProcess;
                }
            }
            PruneFreeList();
            usedRectangles.Add(newNode);
            return newNode;
        }
        public void Insert(List<Rect> rects, List<Rect> dst, FreeRectChoiceHeuristic method) {
            dst.Clear();
            while (rects.Count > 0) {
                int bestScore1 = int.MaxValue;
                int bestScore2 = int.MaxValue;
                int bestRectIndex = -1;
                Rect bestNode = new Rect();
                for (int i = 0; i < rects.Count; ++i) {
                    int score1 = 0;
                    int score2 = 0;
                    Rect newNode = ScoreRect((int)rects[i].width, (int)rects[i].height, method, ref score1, ref score2);
                    if (score1 < bestScore1 || (score1 == bestScore1 && score2 < bestScore2)) {
                        bestScore1 = score1;
                        bestScore2 = score2;
                        bestNode = newNode;
                        bestRectIndex = i;
                    }
                }
                if (bestRectIndex == -1)
                    return;
                PlaceRect(bestNode);
                rects.RemoveAt(bestRectIndex);
            }
        }
        void PlaceRect(Rect node) {
            int numRectanglesToProcess = freeRectangles.Count;
            for (int i = 0; i < numRectanglesToProcess; ++i) {
                if (SplitFreeNode(freeRectangles[i], ref node)) {
                    freeRectangles.RemoveAt(i);
                    --i;
                    --numRectanglesToProcess;
                }
            }
            PruneFreeList();
            usedRectangles.Add(node);
        }
        Rect ScoreRect(int width, int height, FreeRectChoiceHeuristic method, ref int score1, ref int score2) {
            Rect newNode = new Rect();
            score1 = int.MaxValue;
            score2 = int.MaxValue;
            switch (method) {
                case FreeRectChoiceHeuristic.RectBestShortSideFit: newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2); break;
                case FreeRectChoiceHeuristic.RectBottomLeftRule: newNode = FindPositionForNewNodeBottomLeft(width, height, ref score1, ref score2); break;
                case FreeRectChoiceHeuristic.RectContactPointRule:
                    newNode = FindPositionForNewNodeContactPoint(width, height, ref score1);
                    score1 = -score1; // Reverse since we are minimizing, but for contact point score bigger is better.
                    break;
                case FreeRectChoiceHeuristic.RectBestLongSideFit: newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, ref score1); break;
                case FreeRectChoiceHeuristic.RectBestAreaFit: newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2); break;
            }
            // Cannot fit the current rectangle.
            if (newNode.height == 0) {
                score1 = int.MaxValue;
                score2 = int.MaxValue;
            }
            return newNode;
        }
        /// Computes the ratio of used surface area.
        // public float Occupancy() {
        //     ulong usedSurfaceArea = 0;
        //     for (int i = 0; i < usedRectangles.Count; ++i)
        //         usedSurfaceArea += (uint)usedRectangles[i].width * (uint)usedRectangles[i].height;
        //     return (float)usedSurfaceArea / (binWidth * binHeight);
        // }
        Rect FindPositionForNewNodeBottomLeft(int width, int height, ref int bestY, ref int bestX) {  //优先匹配最左下角
            Rect bestNode = new Rect(); //先定义最佳的矩形
            bestY = int.MaxValue;
            for (int i = 0; i < freeRectangles.Count; ++i) { //遍历空闲矩形列表
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) { //若查找到一个长宽都大于目标的长宽的矩形
                    int topSideY = (int)freeRectangles[i].y + height; //上边界等于查找到的空闲矩形的y坐标 + 目标矩形的高度
                    if (topSideY < bestY || (topSideY == bestY && freeRectangles[i].x < bestX)) { //@TODO:
                        bestNode.x = freeRectangles[i].x; //寻找的矩形左下点 x坐标为符合条件的
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestY = topSideY;
                        bestX = (int)freeRectangles[i].x;
                    }
                }
                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    int topSideY = (int)freeRectangles[i].y + width;
                    if (topSideY < bestY || (topSideY == bestY && freeRectangles[i].x < bestX)) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestY = topSideY;
                        bestX = (int)freeRectangles[i].x;
                    }
                }
            }
            return bestNode;
        }
        Rect FindPositionForNewNodeBestShortSideFit(int width, int height, ref int bestShortSideFit, ref int bestLongSideFit) {
            Rect bestNode = new Rect();
            //memset(&bestNode, 0, sizeof(Rect));
            bestShortSideFit = int.MaxValue;
            for (int i = 0; i < freeRectangles.Count; ++i) {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) {
                    int leftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - width);
                    int leftoverVert = Mathf.Abs((int)freeRectangles[i].height - height);
                    int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    int longSideFit = Mathf.Max(leftoverHoriz, leftoverVert);
                    if (shortSideFit < bestShortSideFit || (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit)) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    int flippedLeftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - height);
                    int flippedLeftoverVert = Mathf.Abs((int)freeRectangles[i].height - width);
                    int flippedShortSideFit = Mathf.Min(flippedLeftoverHoriz, flippedLeftoverVert);
                    int flippedLongSideFit = Mathf.Max(flippedLeftoverHoriz, flippedLeftoverVert);
                    if (flippedShortSideFit < bestShortSideFit || (flippedShortSideFit == bestShortSideFit && flippedLongSideFit < bestLongSideFit)) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestShortSideFit = flippedShortSideFit;
                        bestLongSideFit = flippedLongSideFit;
                    }
                }
            }
            return bestNode;
        }
        Rect FindPositionForNewNodeBestLongSideFit(int width, int height, ref int bestShortSideFit, ref int bestLongSideFit) {
            Rect bestNode = new Rect();
            //memset(&bestNode, 0, sizeof(Rect));
            bestLongSideFit = int.MaxValue;
            for (int i = 0; i < freeRectangles.Count; ++i) {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) {
                    int leftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - width);
                    int leftoverVert = Mathf.Abs((int)freeRectangles[i].height - height);
                    int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    int longSideFit = Mathf.Max(leftoverHoriz, leftoverVert);
                    if (longSideFit < bestLongSideFit || (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit)) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    int leftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - height);
                    int leftoverVert = Mathf.Abs((int)freeRectangles[i].height - width);
                    int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    int longSideFit = Mathf.Max(leftoverHoriz, leftoverVert);
                    if (longSideFit < bestLongSideFit || (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit)) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }
            return bestNode;
        }
        Rect FindPositionForNewNodeBestAreaFit(int width, int height, ref int bestAreaFit, ref int bestShortSideFit) {
            Rect bestNode = new Rect();
            //memset(&bestNode, 0, sizeof(Rect));
            bestAreaFit = int.MaxValue;
            for (int i = 0; i < freeRectangles.Count; ++i) {
                int areaFit = (int)freeRectangles[i].width * (int)freeRectangles[i].height - width * height;
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) {
                    int leftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - width);
                    int leftoverVert = Mathf.Abs((int)freeRectangles[i].height - height);
                    int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit)) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }
                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    int leftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - height);
                    int leftoverVert = Mathf.Abs((int)freeRectangles[i].height - width);
                    int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit)) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }
            }
            return bestNode;
        }
        /// Returns 0 if the two intervals i1 and i2 are disjoint, or the length of their overlap otherwise.
        int CommonIntervalLength(int i1start, int i1end, int i2start, int i2end) {
            if (i1end < i2start || i2end < i1start)
                return 0;
            return Mathf.Min(i1end, i2end) - Mathf.Max(i1start, i2start);
        }
        int ContactPointScoreNode(int x, int y, int width, int height) {
            int score = 0;
            if (x == 0 || x + width == binWidth)
                score += height;
            if (y == 0 || y + height == binHeight)
                score += width;
            for (int i = 0; i < usedRectangles.Count; ++i) {
                if (usedRectangles[i].x == x + width || usedRectangles[i].x + usedRectangles[i].width == x)
                    score += CommonIntervalLength((int)usedRectangles[i].y, (int)usedRectangles[i].y + (int)usedRectangles[i].height, y, y + height);
                if (usedRectangles[i].y == y + height || usedRectangles[i].y + usedRectangles[i].height == y)
                    score += CommonIntervalLength((int)usedRectangles[i].x, (int)usedRectangles[i].x + (int)usedRectangles[i].width, x, x + width);
            }
            return score;
        }
        Rect FindPositionForNewNodeContactPoint(int width, int height, ref int bestContactScore) {
            Rect bestNode = new Rect();
            bestContactScore = -1;
            for (int i = 0; i < freeRectangles.Count; ++i) {
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) {
                    int score = ContactPointScoreNode((int)freeRectangles[i].x, (int)freeRectangles[i].y, width, height);
                    if (score > bestContactScore) {
                        bestNode.x = (int)freeRectangles[i].x;
                        bestNode.y = (int)freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestContactScore = score;
                    }
                }
                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    int score = ContactPointScoreNode((int)freeRectangles[i].x, (int)freeRectangles[i].y, height, width);
                    if (score > bestContactScore) {
                        bestNode.x = (int)freeRectangles[i].x;
                        bestNode.y = (int)freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestContactScore = score;
                    }
                }
            }
            return bestNode;
        }
        bool SplitFreeNode(Rect freeNode, ref Rect usedNode) {
            // Test with SAT if the rectangles even intersect.
            if (usedNode.x >= freeNode.x + freeNode.width || usedNode.x + usedNode.width <= freeNode.x ||
                usedNode.y >= freeNode.y + freeNode.height || usedNode.y + usedNode.height <= freeNode.y)
                return false;
            if (usedNode.x < freeNode.x + freeNode.width && usedNode.x + usedNode.width > freeNode.x) {
                // New node at the top side of the used node.
                if (usedNode.y > freeNode.y && usedNode.y < freeNode.y + freeNode.height) {
                    Rect newNode = freeNode;
                    newNode.height = usedNode.y - newNode.y;
                    freeRectangles.Add(newNode);
                }
                // New node at the bottom side of the used node.
                if (usedNode.y + usedNode.height < freeNode.y + freeNode.height) {
                    Rect newNode = freeNode;
                    newNode.y = usedNode.y + usedNode.height;
                    newNode.height = freeNode.y + freeNode.height - (usedNode.y + usedNode.height);
                    freeRectangles.Add(newNode);
                }
            }
            if (usedNode.y < freeNode.y + freeNode.height && usedNode.y + usedNode.height > freeNode.y) {
                // New node at the left side of the used node.
                if (usedNode.x > freeNode.x && usedNode.x < freeNode.x + freeNode.width) {
                    Rect newNode = freeNode;
                    newNode.width = usedNode.x - newNode.x;
                    freeRectangles.Add(newNode);
                }
                // New node at the right side of the used node.
                if (usedNode.x + usedNode.width < freeNode.x + freeNode.width) {
                    Rect newNode = freeNode;
                    newNode.x = usedNode.x + usedNode.width;
                    newNode.width = freeNode.x + freeNode.width - (usedNode.x + usedNode.width);
                    freeRectangles.Add(newNode);
                }
            }
            return true;
        }
        void PruneFreeList() {
            for (int i = 0; i < freeRectangles.Count; ++i)
                for (int j = i + 1; j < freeRectangles.Count; ++j) {
                    if (IsContainedIn(freeRectangles[i], freeRectangles[j])) {
                        freeRectangles.RemoveAt(i);
                        --i;
                        break;
                    }
                    if (IsContainedIn(freeRectangles[j], freeRectangles[i])) {
                        freeRectangles.RemoveAt(j);
                        --j;
                    }
                }
        }

        bool IsContainedIn(Rect a, Rect b) {
            return a.x >= b.x && a.y >= b.y && a.x + a.width <= b.x + b.width && a.y + a.height <= b.y + b.height;
        }
        private static int Log2Floor(int n) { //贴图宽高
            if (n == 0)
                return -1;
            int log = 0;
            int value = n;
            for (int i = 4; i >= 0; --i) { //
                int shift = (1 << i);
                int x = value >> shift;
                if (x != 0) {
                    value = x;
                    log += shift;
                }
            }
            return log;
        }
        private static int Log2Ceiling(int n) { //传入贴图的宽或高
            if (n == 0) {
                return -1;
            } else {
                return 1 + Log2Floor(n - 1);
            }
        }
        private static int LogPow(int i) { //传入一个int值,是贴图的宽或高
            return (int)Math.Pow(2, Log2Ceiling(i)); //返回2的
        }
        //外部调用的主方法,打包贴图 textures是一个Texture2D列表,包含一个图集需要包含的所有贴图数据,最终将这个贴图数据列表的所有贴图数据合并成一张大贴图
        //textures是排好序的,排序规则是矩形的宽高最大值越大,越在数组的前面,保证处理贴图数据的顺序是从较大贴图开始处理的
        public static Rect[] PackTextures(Texture2D texture, Texture2D[] textures) {
            int maxWidth = LogPow(textures[0].width);//寻找最接近宽度的2的n次方的数值
            int maxHeight = LogPow(textures[0].height);//寻找最接近长度的2的n次方的数值
            Rect[] aryRects = CalcByGrowHeight(textures, ref maxWidth, ref maxHeight);
            //将纹理合成在一起
            texture.Resize(maxWidth, maxHeight);
            texture.SetPixels(new Color[maxWidth * maxHeight]);
            Rect[] rects = new Rect[textures.Length];
            //记录rect，以便让unity的sprite meta使用
            for (int i = 0; i < textures.Length; i++) {
                Texture2D tex = textures[i];
                if (!tex) continue;
                Rect rect = aryRects[i];
                Color[] colors = tex.GetPixels();
                texture.SetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height, colors);
                rect.x /= maxWidth;
                rect.y /= maxHeight;
                rect.width = rect.width / maxWidth;
                rect.height = rect.height / maxHeight;
                rects[i] = rect;
            }
            texture.Apply();
            return rects;
        }
        /// 向高度增长空间
        private static Rect[] CalcByGrowHeight(Texture2D[] textures, ref int maxWidth, ref int maxHeight) {
            bool successed = true;
            Rect[] aryRects = new Rect[textures.Length];
            while (true) {
                MaxRectsBinPack packer = new MaxRectsBinPack(maxWidth, maxHeight, false);
                for (int i = 0; i < textures.Length; ++i) {
                    Texture2D tex = textures[i];
                    Rect rect = packer.Insert(tex.width, tex.height, FreeRectChoiceHeuristic.RectBestAreaFit);
                    aryRects[i] = rect;
                    if (rect.width == 0 || rect.height == 0) {
                        //不通过
                        successed = false;
                        break;
                    }
                    if (i == textures.Length - 1) {
                        successed = true;
                    }
                }
                if (successed) {
                    break;
                }
                //先往高度发展，如果发现高度大于1024，则向宽度发展
                if (maxHeight >= ATLAS_MAX_SIZE && maxWidth < ATLAS_MAX_SIZE) {
                    //不允许超过2048
                    maxWidth *= 2;
                } else if (maxHeight >= FAVOR_ATLAS_SIZE && maxWidth < FAVOR_ATLAS_SIZE) {
                    //尽量不能超过1024
                    maxWidth *= 2;
                } else if (maxHeight >= maxWidth * 2) {
                    //尽量使宽和高之间的比例不要相差太大，防止IOS上会扩展成更大的正方形
                    maxWidth *= 2;
                } else {
                    maxHeight *= 2;
                }
                aryRects = new Rect[textures.Length];
            }
            return aryRects;
        }
    }
}