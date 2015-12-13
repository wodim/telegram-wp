//using System.Collections.Generic;
//using System.Linq;
//using TelegramClient.Models;
//using Vk.Api.Models;
//using Vk.Api.Services;

//namespace Vk.Messenger.Models
//{
//    public class UsersByFirstName : List<UsersInGroup>
//    {
//        private readonly Dictionary<string, UsersInGroup> _groups; 

//        private const string Groups = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя#abcdefghijklmnopqrstuvwxyz";

//        public int SumCount
//        {
//            get 
//            { 
//                var count = 0;
//                ForEach(x => count += x.Count);
//                return count;
//            }
//        }

//        public void AddUser(User user)
//        {
//            var key = User.GetFirstNameKey(user);
//            var hasItem = _groups[key].IndexOf(user) != -1;
//            if (!hasItem)
//            {
//                _groups[key].Add(user);
//                _groups[key].Sort(User.CompareByFirstName);
//            }
//        }

//        public void RemoveUser(User user)
//        {
//            var key = User.GetFirstNameKey(user);
//            _groups[key].Remove(user);
//        }

//        public UsersByFirstName(int hintsCount = 0, bool online = false, Dictionary<int, int> excludeUids = null)
//        {
//            var people = new List<User>(CacheService.Database.GetRecords<User>().Where(x => x.IsFriend && (online && x.Online || !online) && (excludeUids == null || !excludeUids.ContainsKey(x.Uid))));
//            people.Sort(User.CompareByFirstName);

//            _groups = new Dictionary<string, UsersInGroup>();

//            if (hintsCount > 0)
//            {
//                var hints = people.OrderBy(x => x.Hint).Take(hintsCount).ToList();
//                var hintsGroup = new UsersInGroup("hints");
//                Add(hintsGroup);
//                _groups["hints"] = hintsGroup;
//                foreach (var hint in hints)
//                {
//                    _groups["hints"].Add(hint);
//                }
//            }

//            foreach (var c in Groups)
//            {
//                var group = new UsersInGroup(c.ToString());
//                Add(group);
//                _groups[c.ToString()] = group;
//            }

//            foreach (var person in people)
//            {
//                person.SortingMode = FullNameSortingMode.FirstName;
//                person.Modify();
//                _groups[User.GetFirstNameKey(person)].Add(person);
//            }

//            CacheService.Database.Storage.Commit();
//        }
//    }
//}
