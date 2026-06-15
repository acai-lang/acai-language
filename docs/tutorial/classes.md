# Classes

Classes let you bundle data and setup code.

## Define a class
```acai
make class Person (name, age = 0) then
    set self.name to name
    set self.age to age
    show "Created {self.name} age {self.age}"
end
```

## Create an instance
```acai
set p to call Person("John", 28)
show "Name: {p.name}"
```

Classes use `self` to access the current instance.

![Class Overview](../images/class-overview.png)

- [Data Structures](datastructure.md)
- [Errors](errors.md)
