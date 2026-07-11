using System;

namespace CodePlanner
{
    public class ProjectTypeComboItem
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }
}
