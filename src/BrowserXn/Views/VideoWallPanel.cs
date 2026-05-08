using System.Windows;
using System.Windows.Controls;
using GDD.ViewModels;

namespace GDD.Views;

public class VideoWallPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(
            double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 600 : availableSize.Height);

        foreach (UIElement child in InternalChildren)
            child.Measure(size);

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0) return finalSize;

        double W = finalSize.Width;
        double H = finalSize.Height;
        if (W < 1 || H < 1) return finalSize;

        int n = InternalChildren.Count;
        var elements = new UIElement[n];
        var ars = new double[n];

        for (int i = 0; i < n; i++)
        {
            elements[i] = InternalChildren[i];
            ars[i] = 0.46;
            if (elements[i] is FrameworkElement fe && fe.DataContext is BrowserCellViewModel vm)
            {
                var d = vm.SelectedDevice;
                ars[i] = (double)(d.Width + 4) / (d.Height + 40);
            }
        }

        var rowOrder = Enumerable.Range(0, n).OrderBy(i => ars[i]).ToArray();
        var colOrder = Enumerable.Range(0, n).OrderByDescending(i => ars[i]).ToArray();

        int bestRowK = 1;
        double bestRowUtil = 0;

        for (int k = 1; k <= n; k++)
        {
            double totalH = GroupsTotalDim(rowOrder, ars, W, k, isRow: true);
            if (totalH <= H * 1.001)
            {
                double util = totalH / H;
                if (util > bestRowUtil)
                {
                    bestRowUtil = util;
                    bestRowK = k;
                }
            }
        }

        int bestColK = 1;
        double bestColUtil = 0;

        for (int k = 1; k <= n; k++)
        {
            double totalW = GroupsTotalDim(colOrder, ars, H, k, isRow: false);
            if (totalW <= W * 1.001)
            {
                double util = totalW / W;
                if (util > bestColUtil)
                {
                    bestColUtil = util;
                    bestColK = k;
                }
            }
        }

        if (bestRowUtil >= bestColUtil)
            ArrangeGroups(elements, rowOrder, ars, W, H, bestRowK, isRow: true);
        else
            ArrangeGroups(elements, colOrder, ars, W, H, bestColK, isRow: false);

        return finalSize;
    }

    private double GroupsTotalDim(int[] order, double[] ars, double crossDim, int groupCount, bool isRow)
    {
        int n = order.Length;
        int baseSize = n / groupCount;
        int extra = n % groupCount;
        double total = 0;
        int idx = 0;

        for (int g = 0; g < groupCount; g++)
        {
            int size = baseSize + (g < extra ? 1 : 0);
            double sum = 0;

            for (int i = 0; i < size; i++)
            {
                if (isRow)
                    sum += ars[order[idx + i]];
                else
                    sum += 1.0 / ars[order[idx + i]];
            }

            total += crossDim / sum;
            idx += size;
        }

        return total;
    }

    private void ArrangeGroups(UIElement[] elements, int[] order, double[] ars,
        double W, double H, int groupCount, bool isRow)
    {
        int n = order.Length;
        int baseSize = n / groupCount;
        int extra = n % groupCount;

        double crossDim = isRow ? W : H;
        double totalDim = GroupsTotalDim(order, ars, crossDim, groupCount, isRow);
        double mainLimit = isRow ? H : W;
        double offset = Math.Max(0, (mainLimit - totalDim) / 2);

        double pos = offset;
        int idx = 0;

        for (int g = 0; g < groupCount; g++)
        {
            int size = baseSize + (g < extra ? 1 : 0);
            double sum = 0;

            for (int i = 0; i < size; i++)
            {
                if (isRow)
                    sum += ars[order[idx + i]];
                else
                    sum += 1.0 / ars[order[idx + i]];
            }

            double groupDim = crossDim / sum;
            double cross = 0;

            for (int i = 0; i < size; i++)
            {
                int ei = order[idx + i];
                double itemCross, itemMain;

                if (isRow)
                {
                    itemMain = groupDim;
                    itemCross = groupDim * ars[ei];
                    elements[ei].Arrange(new Rect(cross, pos, itemCross, itemMain));
                }
                else
                {
                    itemMain = groupDim;
                    itemCross = groupDim / ars[ei];
                    elements[ei].Arrange(new Rect(pos, cross, itemMain, itemCross));
                }

                cross += itemCross;
            }

            pos += groupDim;
            idx += size;
        }
    }
}
