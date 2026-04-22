using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GestorDocumentoApp.Utils
{
    public static class EnumHelper
    {
        public static IEnumerable<SelectListItem> GetSelectList<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(e => new SelectListItem
                {
                    Text = EnumHelper.GetDisplayName(e),
                    Value = e.ToString()
                });
        }

        public static string GetDisplayName<T>(T value) where T : Enum
        {
            var memberInfo = typeof(T).GetMember(value.ToString()).FirstOrDefault();
            if (memberInfo != null)
            {
                var displayAttribute = memberInfo.GetCustomAttribute<DisplayAttribute>();
                if (displayAttribute != null)
                    return displayAttribute.GetName() ?? value.ToString();
            }

            return value.ToString();
        }

        public static string GetDisplayNames(this Enum value)
        {
            var memberInfo = value.GetType().GetMember(value.ToString()).FirstOrDefault();

            if (memberInfo != null)
            {
                var displayAttribute = memberInfo.GetCustomAttribute<DisplayAttribute>();
                if (displayAttribute != null)
                    return displayAttribute.GetName() ?? value.ToString();
            }

            return value.ToString();

        }
    }
}
