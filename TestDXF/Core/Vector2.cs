﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;

namespace Dxf
{
    public class Vector2
    {
        public double X { get; private set; }
        public double Y { get; private set; }

        public Vector2(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
