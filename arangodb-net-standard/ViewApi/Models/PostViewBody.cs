namespace ArangoDBNetStandard.ViewApi.Models
{
    public class PostViewBody
    {
        public string Name { get; set; }
        public string CollectionName { get; set; }
        public string[] FieldsToIndex { get; set; }
    }
}