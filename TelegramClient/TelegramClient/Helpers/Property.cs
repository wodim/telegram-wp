using System;
using System.Linq;
using System.Linq.Expressions;

namespace TelegramClient.Helpers
{
    public static class Property
    {
        public static bool NameEquals(string name1, string name2)
        {
            return string.Equals(name1, name2, StringComparison.OrdinalIgnoreCase);
        }

        public static bool NameEquals<T>(string name, Expression<Func<T>> propertySelector)  
        {
            var memberExpression = propertySelector.Body as MemberExpression;
            if (memberExpression != null)
            {
                return NameEquals(name, memberExpression.Member.Name);
            }

            return false;
        }

        public static bool AnyNameEquals<T>(string name, params Expression<Func<T>>[] propertySelectors)
        {
            return propertySelectors.Select(propertySelector => propertySelector.Body as MemberExpression).Any(memberExpression => memberExpression != null && NameEquals(name, memberExpression.Member.Name));
        }

        public static bool NameEquals<T>(Expression<Func<T>> propertySelector, string name)
        {
            return NameEquals(name, propertySelector);
        }


        public static bool NameEquals<T1, T2>(Expression<Func<T1>> propertySelector1, Expression<Func<T2>> propertySelector2)
        {
            var memberExpression = propertySelector1.Body as MemberExpression;
            if (memberExpression != null)
            {
                return NameEquals(memberExpression.Member.Name, propertySelector2);
            }

            return false;
        }
    }
}
