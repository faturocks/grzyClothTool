using grzyClothTool.Models;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Texture;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace grzyClothTool.Extensions;

public static class ObservableCollectionExtensions
{
    public static void Sort(this ObservableCollection<GDrawable> drawables, bool shouldReassignNumbers = false)
    {
        var sorted = drawables.OrderBy(x => x.Sex)
                              .ThenBy(x => x.Name)
                              .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            if (shouldReassignNumbers)
            {
                sorted[i].Number = sorted.Take(i).Count(x => x.TypeNumeric == sorted[i].TypeNumeric && x.IsProp == sorted[i].IsProp && x.Sex == sorted[i].Sex);
                sorted[i].SetDrawableName();
            }
            drawables.Move(drawables.IndexOf(sorted[i]), i);
        }
    }

    public static void ReassignNumbers(this ObservableCollection<GDrawable> drawables, GDrawable drawable)
    {
        int counter = 0;

        // Get drawables of the same type and sex, ordered by their current position in the collection
        var sameTypeDrawables = drawables
            .Where(x => x.IsProp == drawable.IsProp && x.Sex == drawable.Sex && x.TypeNumeric == drawable.TypeNumeric)
            .ToList();

        // Reassign numbers based on their current order in the collection
        foreach (var item in sameTypeDrawables)
        {
            item.Number = counter++;
            item.SetDrawableName();
        }
    }

    public static void Sort(this ObservableCollection<Addon> addons, bool shouldReassignNumbers = false)
    {
        foreach (var addon in addons)
        {
            addon.Drawables.Sort(shouldReassignNumbers);
        }
    }

    public static void ReassignNumbers(this ObservableCollection<GTexture> textures)
    {
        for (int i = 0; i < textures.Count; i++)
        {
            textures[i].TxtNumber = i;
        }
    }

    public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source)
    {
        return new ObservableCollection<T>(source);
    }
}
