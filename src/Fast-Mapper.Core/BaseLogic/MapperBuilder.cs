using Fast_Mapper.Core.Abstractions;

namespace Fast_Mapper.Core.BaseLogic
{
    /// <summary>
    /// Fluent builder for configuring mappings and producing an <see cref="IMapper"/> instance.
    /// </summary>
    public class MapperBuilder
    {
        private readonly MapperConfig _config = new MapperConfig();

        private MapperBuilder() { }

        /// <summary>
        /// Creates a new <see cref="MapperBuilder"/>.
        /// </summary>
        public static MapperBuilder Create() => new MapperBuilder();

        /// <summary>
        /// Begins configuration for mapping between <typeparamref name="TSource"/> and <typeparamref name="TDestination"/>.
        /// </summary>
        public TypeMapBuilder<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var map = _config.CreateMap<TSource, TDestination>();
            return new TypeMapBuilder<TSource, TDestination>(map, this);
        }

        /// <summary>
        /// Builds the configured <see cref="IMapper"/>.
        /// </summary>
        public IMapper Build() => _config.BuildMapper();

        /// <summary>
        /// Returns the underlying <see cref="MapperConfig"/> for advanced scenarios.
        /// </summary>
        public MapperConfig BuildConfig() => _config;

        internal MapperBuilder _Continue() => this;
    }

    /// <summary>
    /// Fluent helper to configure a specific TypeMap returned by <see cref="MapperBuilder.CreateMap{TSource,TDestination}"/>.
    /// </summary>
    public sealed class TypeMapBuilder<TSource, TDestination>
    {
        private readonly TypeMap<TSource, TDestination> _map;
        private readonly MapperBuilder _parent;

        internal TypeMapBuilder(TypeMap<TSource, TDestination> map, MapperBuilder parent)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }

        /// <summary>
        /// Specify a custom converter for the whole mapping.
        /// </summary>
        public TypeMapBuilder<TSource, TDestination> ConvertUsing(Func<TSource, TDestination> converter)
        {
            _map.ConvertUsing(converter ?? throw new ArgumentNullException(nameof(converter)));
            return this;
        }

        /// <summary>
        /// Specify a resolver for a destination member.
        /// </summary>
        public TypeMapBuilder<TSource, TDestination> ForMember<TMember>(System.Linq.Expressions.Expression<Func<TDestination, TMember>> destMember, Func<TSource, object> resolver)
        {
            _map.ForMember(destMember ?? throw new ArgumentNullException(nameof(destMember)), resolver ?? throw new ArgumentNullException(nameof(resolver)));
            return this;
        }

        /// <summary>
        /// Ignore a destination member.
        /// </summary>
        public TypeMapBuilder<TSource, TDestination> Ignore<TMember>(System.Linq.Expressions.Expression<Func<TDestination, TMember>> destMember)
        {
            _map.Ignore(destMember ?? throw new ArgumentNullException(nameof(destMember)));
            return this;
        }

        /// <summary>
        /// Finish configuring this TypeMap and return the parent builder for chaining.
        /// </summary>
        public MapperBuilder Done() => _parent._Continue();
    }
}

