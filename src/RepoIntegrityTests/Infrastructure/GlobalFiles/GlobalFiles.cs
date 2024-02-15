﻿namespace RepoIntegrityTests
{
    using System;

    public static class GlobalFiles
    {
        static readonly Lazy<CustomBuildProps> customBuildProps = new(new CustomBuildProps());
        public static CustomBuildProps CustomBuildProps => customBuildProps.Value;
    }
}
