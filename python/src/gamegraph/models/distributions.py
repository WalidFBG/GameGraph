import torch


class Bernoulli:
    PARAMETERS = ("probability",)

    @staticmethod
    def log_prob(parameters: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        (probability,) = parameters
        Bernoulli._require_outcomes(outcomes)
        return torch.where(outcomes == 1, torch.log(probability), torch.log1p(-probability))

    @staticmethod
    def sample(parameters: tuple[torch.Tensor, ...], uniforms: torch.Tensor) -> torch.Tensor:
        (probability,) = parameters
        if not ((uniforms >= 0) & (uniforms < 1)).all():
            raise ValueError("A uniform is in [0, 1)")
        return (uniforms < probability).to(torch.int64)

    @staticmethod
    def initial_parameters(outcomes: torch.Tensor) -> tuple[float, ...]:
        Bernoulli._require_outcomes(outcomes)
        mean = float(outcomes.to(torch.float64).mean())
        if mean in (0, 1):
            raise ValueError(f"Every outcome is {int(mean)}, so there is nothing to fit")
        return (mean,)

    @staticmethod
    def _require_outcomes(outcomes: torch.Tensor) -> None:
        if not ((outcomes == 0) | (outcomes == 1)).all():
            raise ValueError("A Bernoulli outcome is 0 or 1")
