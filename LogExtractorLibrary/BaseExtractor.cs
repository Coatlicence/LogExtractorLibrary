namespace LogExtractorLibrary
{
    public abstract class BaseExtractor<T>
    {
        public abstract bool IsValid();
        public abstract Task<T> ExtractAsync();
    }
}
