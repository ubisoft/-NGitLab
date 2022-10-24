﻿using System;
using System.Collections.Generic;
using System.Globalization;

namespace NGitLab.Mock
{
    public static class ProjectExtensions
    {
        public static Project FindProject(this IEnumerable<Project> projects, string idOrPathWithNamespace)
        {
            foreach (var project in projects)
            {
                if (string.Equals(project.Id.ToString(CultureInfo.InvariantCulture), idOrPathWithNamespace, StringComparison.Ordinal))
                    return project;
            }

            foreach (var project in projects)
            {
                if (string.Equals(project.PathWithNamespace, idOrPathWithNamespace, StringComparison.OrdinalIgnoreCase))
                    return project;
            }

            return null;
        }

        public static Project FindById(this IEnumerable<Project> projects, int id)
        {
            foreach (var project in projects)
            {
                if (project.Id == id)
                    return project;
            }

            return null;
        }
    }
}
