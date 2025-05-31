# Alt:V YMT Optimization

## Overview

This implementation includes an optimized splitting algorithm specifically for Alt:V builds that minimizes the number of YMT files generated while respecting game engine limits.

## Game Limits for Alt:V

Based on the [YMT Game Limit documentation](https://github.com/DurtyFree/durty-cloth-tool/wiki/YMT-game-limit-and-crash-issues):

- **~100 YMT files per gender total** (across all addons)
- **128 drawables per type per YMT file** (components like shirts, pants, etc.)
- **Props limit patched** (no 255 limit in Alt:V)
- **High heels limit patched** (no 255 limit in Alt:V)

## Optimization Algorithm

### Before Optimization
The original algorithm created new YMT files whenever any single drawable type reached 128 items, leading to inefficient splitting:

```
YMT 1: Shirts: 128, Pants: 50, Shoes: 30
YMT 2: Shirts: 100, Pants: 0, Shoes: 0  ❌ Inefficient!
```

### After Optimization (Alt:V Only)
The new bin-packing algorithm efficiently groups drawables to minimize YMT count:

```
YMT 1: Shirts: 128, Pants: 128, Shoes: 128, Hats: 50
YMT 2: Shirts: 100, Hats: 78, Accessories: 90  ✅ Optimized!
```

### Algorithm Details

1. **Greedy Bin-Packing**: Tries to fit as many drawables as possible in each YMT
2. **Type-Based Capacity**: Tracks 128-item limit per drawable type per YMT
3. **Mixed Types**: Allows different drawable types in the same YMT file
4. **Comprehensive Logging**: Reports optimization statistics

## Benefits

- **Fewer YMT Files**: Significantly reduces the number of generated YMT files
- **Better Resource Usage**: Makes full use of the 128 per type limit
- **Alt:V Optimized**: Takes advantage of Alt:V's patched prop/heel limits
- **Future-Proof**: Stays well under the ~100 YMT total limit

## Usage

The optimization is automatically applied when:
1. Building for Alt:V resource type
2. "Split addons as separate resources" is enabled
3. Multiple addon files are present

## Implementation

Key files modified:
- `grzyClothTool/Helpers/BuildResourceHelper.cs`: Added `OptimizeDrawableGroupsForAltV()` method
- `grzyClothTool/Views/BuildWindow.xaml`: Updated UI text to indicate optimization
- `grzyClothTool/GlobalConstants.cs`: Updated documentation comments

The optimization preserves compatibility with FiveM and Singleplayer builds, which continue to use the original algorithm. 