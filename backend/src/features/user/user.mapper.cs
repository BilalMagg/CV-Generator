using AutoMapper;
using backend.src.features.user.entity;
using backend.src.features.user.dto;

namespace backend.src.features.user;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserResponseDto>();

        CreateMap<CreateUserDto, User>()
            .ForMember(dest => dest.PasswordHash,
                opt => opt.MapFrom(src => BCrypt.Net.BCrypt.HashPassword(src.Password)));

        CreateMap<UpdateUserDto, User>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}